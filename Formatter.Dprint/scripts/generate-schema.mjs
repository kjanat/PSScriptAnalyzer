import { readFile, writeFile } from 'node:fs/promises';
import process from 'node:process';

const [pluginPath, schemaPath, mode] = process.argv.slice(2);
if (!pluginPath || !schemaPath) {
  console.error('usage: node generate-schema.mjs <plugin.wasm> <schema.json> [--check]');
  process.exit(2);
}

const bytes = await readFile(pluginPath);
const { instance } = await WebAssembly.instantiate(bytes, {
  env: { fd_write: () => 0 },
});
const length = instance.exports.get_config_schema();
const pointer = instance.exports.get_shared_bytes_ptr();
const schema = new TextDecoder().decode(
  new Uint8Array(instance.exports.memory.buffer, pointer, length),
) + '\n';

if (mode === '--check') {
  const existing = await readFile(schemaPath, 'utf8');
  if (existing !== schema) {
    console.error(`${schemaPath} is out of date; regenerate it from plugin.wasm.`);
    process.exit(1);
  }
} else {
  await writeFile(schemaPath, schema);
}
