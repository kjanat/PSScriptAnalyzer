import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import process from 'node:process';

const [pluginPath] = process.argv.slice(2);
if (!pluginPath) {
  console.error('usage: node check-plugin.mjs <plugin.wasm>');
  process.exit(2);
}

const bytes = await readFile(pluginPath);
const module = await WebAssembly.compile(bytes);
assert.deepEqual(WebAssembly.Module.imports(module), [
  { module: 'env', name: 'fd_write', kind: 'function' },
]);

const requiredExports = [
  'memory',
  'dprint_plugin_version_4',
  'clear_shared_bytes',
  'get_shared_bytes_ptr',
  'register_config',
  'release_config',
  'get_config_diagnostics',
  'get_resolved_config',
  'get_config_file_matching',
  'get_plugin_info',
  'get_license_text',
  'set_file_path',
  'set_override_config',
  'format',
  'get_formatted_text',
  'get_error_text',
];
const exports = new Set(WebAssembly.Module.exports(module).map(({ name }) => name));
for (const name of requiredExports) {
  assert(exports.has(name), `missing dprint export: ${name}`);
}

const instance = await WebAssembly.instantiate(module, {
  env: { fd_write: () => 0 },
});
assert.equal(instance.exports.dprint_plugin_version_4(), 4);

const readSharedText = (length) => {
  const pointer = instance.exports.get_shared_bytes_ptr();
  return new TextDecoder().decode(
    new Uint8Array(instance.exports.memory.buffer, pointer, length),
  );
};

const info = JSON.parse(readSharedText(instance.exports.get_plugin_info()));
assert.equal(info.name, 'dprint-plugin-powershell');
assert.equal(info.configKey, 'powershell');
assert.match(info.helpUrl, /^https:\/\//);
assert.match(info.configSchemaUrl, /^https:\/\//);
assert.match(info.updateUrl, /^https:\/\//);

console.log(`validated ${pluginPath} (${bytes.length} bytes)`);
