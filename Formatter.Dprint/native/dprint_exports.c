#include <driver.h>
#include <mono/metadata/appdomain.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/object.h>
#include <mono/utils/mono-publib.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#define DPRINT_EXPORT(name) __attribute__((export_name(name)))

typedef struct ConfigEntry {
    uint32_t id;
    char *json;
    struct ConfigEntry *next;
} ConfigEntry;

static uint8_t *shared_bytes;
static uint32_t shared_capacity;
static uint32_t shared_length;
static ConfigEntry *configs;
static char *file_path;
static char *override_config;
static MonoMethod *format_method;
static MonoMethod *diagnostics_method;
static MonoMethod *resolved_config_method;
static MonoMethod *schema_method;
static const char *error_text;
static char *owned_error_text;
static int runtime_state;

const char *dotnet_wasi_getentrypointassemblyname(void);

static uint32_t write_shared_bytes(const uint8_t *bytes, uint32_t length) {
    if (length > shared_capacity) {
        uint8_t *resized = realloc(shared_bytes, length);
        if (resized == NULL) {
            error_text = "Could not allocate the dprint shared buffer.";
            return 0;
        }
        shared_bytes = resized;
        shared_capacity = length;
    }
    if (length > 0) {
        memcpy(shared_bytes, bytes, length);
    }
    shared_length = length;
    return length;
}

static uint32_t write_shared(const char *text) {
    return write_shared_bytes((const uint8_t *)text, (uint32_t)strlen(text));
}

static void set_owned_error(const char *text) {
    free(owned_error_text);
    owned_error_text = strdup(text);
    error_text = owned_error_text == NULL ? "Could not allocate the formatter error message." : owned_error_text;
}

static char *copy_shared_string(void) {
    char *copy = malloc((size_t)shared_length + 1);
    if (copy == NULL) {
        return NULL;
    }
    if (shared_length > 0) {
        memcpy(copy, shared_bytes, shared_length);
    }
    copy[shared_length] = '\0';
    return copy;
}

static ConfigEntry *find_config(uint32_t id) {
    for (ConfigEntry *entry = configs; entry != NULL; entry = entry->next) {
        if (entry->id == id) {
            return entry;
        }
    }
    return NULL;
}

static int is_valid_utf8(const uint8_t *bytes, uint32_t length) {
    uint32_t index = 0;
    while (index < length) {
        uint8_t byte = bytes[index++];
        if (byte <= 0x7f) {
            if (byte == 0) {
                return 0;
            }
            continue;
        }

        uint32_t remaining;
        uint32_t codepoint;
        if ((byte & 0xe0) == 0xc0) {
            remaining = 1;
            codepoint = byte & 0x1f;
            if (codepoint < 2) {
                return 0;
            }
        } else if ((byte & 0xf0) == 0xe0) {
            remaining = 2;
            codepoint = byte & 0x0f;
        } else if ((byte & 0xf8) == 0xf0) {
            remaining = 3;
            codepoint = byte & 0x07;
        } else {
            return 0;
        }

        if (index + remaining > length) {
            return 0;
        }
        for (uint32_t offset = 0; offset < remaining; offset++) {
            uint8_t continuation = bytes[index++];
            if ((continuation & 0xc0) != 0x80) {
                return 0;
            }
            codepoint = (codepoint << 6) | (continuation & 0x3f);
        }
        if ((remaining == 2 && codepoint < 0x800) ||
            (remaining == 3 && codepoint < 0x10000) ||
            (codepoint >= 0xd800 && codepoint <= 0xdfff) ||
            codepoint > 0x10ffff) {
            return 0;
        }
    }
    return 1;
}

static int ensure_runtime(void) {
    if (runtime_state != 0) {
        return runtime_state > 0;
    }
    runtime_state = -1;
    mono_wasm_load_runtime("", 0);

    MonoAssembly *assembly = mono_assembly_open(dotnet_wasi_getentrypointassemblyname(), NULL);
    if (assembly == NULL) {
        error_text = "Could not load the embedded Formatter.Dprint assembly.";
        return 0;
    }
    MonoClass *klass = mono_wasm_assembly_find_class(
        assembly,
        "Microsoft.PowerShell.ScriptAnalyzer.Formatter.Dprint",
        "Plugin"
    );
    if (klass == NULL) {
        error_text = "Could not find the managed dprint Plugin type.";
        return 0;
    }
    format_method = mono_wasm_assembly_find_method(klass, "Format", 3);
    diagnostics_method = mono_wasm_assembly_find_method(klass, "GetConfigDiagnostics", 1);
    resolved_config_method = mono_wasm_assembly_find_method(klass, "GetResolvedConfig", 1);
    schema_method = mono_wasm_assembly_find_method(klass, "GetConfigSchema", 0);
    if (format_method == NULL || diagnostics_method == NULL || resolved_config_method == NULL || schema_method == NULL) {
        error_text = "Could not find the managed dprint formatter entry point.";
        return 0;
    }
    runtime_state = 1;
    return 1;
}

static char *invoke_managed_no_args(MonoMethod *method) {
    MonoObject *exception = NULL;
    MonoObject *result = mono_runtime_invoke(method, NULL, NULL, &exception);
    if (exception != NULL || result == NULL) {
        return NULL;
    }
    return mono_string_to_utf8((MonoString *)result);
}

static char *invoke_managed_string(MonoMethod *method, const char *input) {
    MonoString *managed_input = mono_string_new(mono_domain_get(), input);
    void *arguments[] = { managed_input };
    MonoObject *exception = NULL;
    MonoObject *result = mono_runtime_invoke(method, NULL, arguments, &exception);
    if (exception != NULL || result == NULL) {
        return NULL;
    }
    return mono_string_to_utf8((MonoString *)result);
}

DPRINT_EXPORT("dprint_plugin_version_4")
uint32_t dprint_plugin_version_4(void) {
    return 4;
}

DPRINT_EXPORT("clear_shared_bytes")
uint32_t clear_shared_bytes(uint32_t capacity) {
    if (capacity > shared_capacity) {
        uint8_t *resized = realloc(shared_bytes, capacity);
        if (resized == NULL) {
            error_text = "Could not allocate the dprint shared buffer.";
            return 0;
        }
        shared_bytes = resized;
        shared_capacity = capacity;
    }
    shared_length = capacity;
    return (uint32_t)(uintptr_t)shared_bytes;
}

DPRINT_EXPORT("get_shared_bytes_ptr")
uint32_t get_shared_bytes_ptr(void) {
    return (uint32_t)(uintptr_t)shared_bytes;
}

DPRINT_EXPORT("register_config")
void register_config(uint32_t config_id) {
    ConfigEntry *entry = find_config(config_id);
    if (entry == NULL) {
        entry = calloc(1, sizeof(ConfigEntry));
        if (entry == NULL) {
            error_text = "Could not allocate a dprint configuration.";
            return;
        }
        entry->id = config_id;
        entry->next = configs;
        configs = entry;
    }
    free(entry->json);
    entry->json = copy_shared_string();
}

DPRINT_EXPORT("release_config")
void release_config(uint32_t config_id) {
    ConfigEntry **cursor = &configs;
    while (*cursor != NULL) {
        if ((*cursor)->id == config_id) {
            ConfigEntry *removed = *cursor;
            *cursor = removed->next;
            free(removed->json);
            free(removed);
            return;
        }
        cursor = &(*cursor)->next;
    }
}

DPRINT_EXPORT("get_config_diagnostics")
uint32_t get_config_diagnostics(uint32_t config_id) {
    ConfigEntry *entry = find_config(config_id);
    const char *config_json = entry != NULL && entry->json != NULL ? entry->json : "{}";
    if (!ensure_runtime()) {
        return write_shared("[{\"propertyName\":\"\",\"message\":\"Could not initialize the managed formatter.\"}]");
    }
    char *diagnostics = invoke_managed_string(diagnostics_method, config_json);
    if (diagnostics == NULL) {
        return write_shared("[{\"propertyName\":\"\",\"message\":\"Managed configuration validation failed.\"}]");
    }
    uint32_t length = write_shared(diagnostics);
    mono_free(diagnostics);
    return length;
}

DPRINT_EXPORT("get_resolved_config")
uint32_t get_resolved_config(uint32_t config_id) {
    ConfigEntry *entry = find_config(config_id);
    const char *config_json = entry != NULL && entry->json != NULL ? entry->json : "{}";
    if (!ensure_runtime()) {
        return write_shared("{}");
    }
    char *resolved = invoke_managed_string(resolved_config_method, config_json);
    if (resolved == NULL) {
        return write_shared("{}");
    }
    uint32_t length = write_shared(resolved);
    mono_free(resolved);
    return length;
}

DPRINT_EXPORT("get_config_file_matching")
uint32_t get_config_file_matching(uint32_t config_id) {
    (void)config_id;
    return write_shared("{\"fileExtensions\":[\"ps1\",\"psm1\",\"psd1\"],\"fileNames\":[]}");
}

DPRINT_EXPORT("get_config_schema")
uint32_t get_config_schema(void) {
    if (!ensure_runtime()) {
        return write_shared("{}");
    }
    char *schema = invoke_managed_no_args(schema_method);
    if (schema == NULL) {
        return write_shared("{}");
    }
    uint32_t length = write_shared(schema);
    mono_free(schema);
    return length;
}

DPRINT_EXPORT("get_plugin_info")
uint32_t get_plugin_info(void) {
    return write_shared(
        "{\"name\":\"dprint-plugin-powershell\",\"version\":\"0.1.0\","
        "\"configKey\":\"powershell\","
        "\"helpUrl\":\"https://github.com/kjanat/PSScriptAnalyzer/tree/wasm-formatter/Formatter.Dprint\","
        "\"configSchemaUrl\":\"https://plugins.dprint.dev/kjanat/PSScriptAnalyzer/0.1.0/schema.json\","
        "\"updateUrl\":\"https://plugins.dprint.dev/kjanat/PSScriptAnalyzer/latest.json\"}"
    );
}

DPRINT_EXPORT("get_license_text")
uint32_t get_license_text(void) {
    return write_shared("MIT License");
}

DPRINT_EXPORT("set_file_path")
void set_file_path(void) {
    free(file_path);
    file_path = copy_shared_string();
}

DPRINT_EXPORT("set_override_config")
void set_override_config(void) {
    free(override_config);
    override_config = copy_shared_string();
}

DPRINT_EXPORT("format")
uint32_t format(uint32_t config_id) {
    free(owned_error_text);
    owned_error_text = NULL;
    error_text = NULL;
    if (!is_valid_utf8(shared_bytes, shared_length)) {
        error_text = "PowerShell source must be valid UTF-8 without NUL bytes.";
        return 2;
    }
    if (!ensure_runtime()) {
        return 2;
    }

    ConfigEntry *entry = find_config(config_id);
    const char *config_json = entry != NULL && entry->json != NULL ? entry->json : "{}";
    char *source = copy_shared_string();
    if (source == NULL) {
        error_text = "Could not copy the PowerShell source.";
        return 2;
    }

    MonoDomain *domain = mono_domain_get();
    MonoString *managed_source = mono_string_new(domain, source);
    MonoString *managed_config = mono_string_new(domain, config_json);
    MonoString *managed_override = mono_string_new(domain, override_config == NULL ? "" : override_config);
    void *arguments[] = { managed_source, managed_config, managed_override };
    MonoObject *exception = NULL;
    MonoObject *result = mono_runtime_invoke(format_method, NULL, arguments, &exception);
    if (exception != NULL) {
        MonoObject *string_exception = NULL;
        MonoString *exception_string = mono_object_to_string(exception, &string_exception);
        if (exception_string != NULL && string_exception == NULL) {
            char *exception_utf8 = mono_string_to_utf8(exception_string);
            if (exception_utf8 != NULL) {
                set_owned_error(exception_utf8);
                mono_free(exception_utf8);
            }
        }
        free(source);
        if (error_text == NULL) {
            error_text = "The managed PowerShell formatter threw an exception.";
        }
        return 2;
    }
    if (result == NULL) {
        free(source);
        error_text = "The managed PowerShell formatter returned no result.";
        return 2;
    }

    char *formatted = mono_string_to_utf8((MonoString *)result);
    if (formatted == NULL) {
        free(source);
        error_text = "The managed PowerShell formatter returned no text.";
        return 2;
    }
    uint32_t formatted_length = (uint32_t)strlen(formatted);
    int changed = formatted_length != shared_length || memcmp(formatted, source, formatted_length) != 0;
    if (changed) {
        write_shared_bytes((const uint8_t *)formatted, formatted_length);
    }
    mono_free(formatted);
    free(source);
    free(override_config);
    override_config = NULL;
    return changed ? 1 : 0;
}

DPRINT_EXPORT("get_formatted_text")
uint32_t get_formatted_text(void) {
    return shared_length;
}

DPRINT_EXPORT("get_error_text")
uint32_t get_error_text(void) {
    return write_shared(error_text == NULL ? "Unknown formatter error." : error_text);
}
