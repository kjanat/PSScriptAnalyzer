use std::path::Path;

use dprint_core::configuration::{ConfigKeyMap, ConfigKeyValue, GlobalConfiguration};
use dprint_core::plugins::{
    FormatConfigId, NullCancellationToken, SyncFormatRequest, SyncPluginHandler,
};
use dprint_plugin_powershell::{Configuration, PowerShellPluginHandler};

fn resolve(
    config: ConfigKeyMap,
) -> dprint_core::plugins::PluginResolveConfigurationResult<Configuration> {
    let mut handler = PowerShellPluginHandler;
    handler.resolve_config(config, &GlobalConfiguration::default())
}

fn format(config: &Configuration, input: &[u8]) -> anyhow::Result<Option<Vec<u8>>> {
    let mut handler = PowerShellPluginHandler;
    let token = NullCancellationToken;
    handler.format(
        SyncFormatRequest {
            file_path: Path::new("test.ps1"),
            file_bytes: input.to_vec(),
            config_id: FormatConfigId::from_raw(1),
            config,
            range: None,
            token: &token,
        },
        |_| Ok(None),
    )
}

#[test]
fn formats_powershell_and_is_idempotent() {
    let resolved = resolve(ConfigKeyMap::new());
    assert!(resolved.diagnostics.is_empty());
    let input = b"IF($x-EQ 1){'yes'}ELSE{'no'}";
    let expected = "if($x -eq 1) {\n    'yes'\n} else {\n    'no'\n}";
    let first = format(&resolved.config, input)
        .unwrap()
        .expect("first pass should change source");
    assert_eq!(String::from_utf8(first.clone()).unwrap(), expected);
    assert!(format(&resolved.config, &first).unwrap().is_none());
}

#[test]
fn supports_next_line_braces_and_global_indentation() {
    let mut config = ConfigKeyMap::new();
    config.insert(
        "braceStyle".into(),
        ConfigKeyValue::String("nextLine".into()),
    );
    config.insert("indentWidth".into(), ConfigKeyValue::Number(2));
    let resolved = resolve(config);
    let output = format(&resolved.config, b"function Test { 'ok' }")
        .unwrap()
        .expect("format should change source");
    assert_eq!(
        String::from_utf8(output).unwrap(),
        "function Test\n{\n  'ok'\n}"
    );
}

#[test]
fn unknown_configuration_is_diagnostic_first() {
    let mut config = ConfigKeyMap::new();
    config.insert("indentation".into(), ConfigKeyValue::Number(2));
    let resolved = resolve(config);
    assert!(
        resolved
            .diagnostics
            .iter()
            .any(|diagnostic| diagnostic.property_name == "indentation")
    );
}

#[test]
fn invalid_utf8_returns_an_error() {
    let resolved = resolve(ConfigKeyMap::new());
    assert!(format(&resolved.config, &[0xff, 0xfe]).is_err());
}

#[test]
fn plugin_info_has_release_urls() {
    let mut handler = PowerShellPluginHandler;
    let info = handler.plugin_info();
    assert!(info.config_schema_url.ends_with("/schema.json"));
    assert!(info.update_url.is_some());
}
