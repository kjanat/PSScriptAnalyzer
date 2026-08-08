use schemars::{JsonSchema, Schema, generate::SchemaSettings};
use serde::Serialize;
use serde_json::{Value, json};

use crate::{BraceStyle, SCHEMA_URL};

#[derive(Clone, Debug, Default, Serialize, JsonSchema)]
#[schemars(
    title = "dprint PowerShell plugin configuration",
    description = "All fields are optional. Indentation options inherit from dprint global configuration."
)]
#[serde(rename_all = "camelCase")]
pub struct DprintPowerShellConfigSchema {
    pub locked: Option<bool>,
    pub brace_style: Option<BraceStyle>,
    #[schemars(range(min = 0, max = 32))]
    pub indent_width: Option<u8>,
    pub use_tabs: Option<bool>,
    pub correct_keyword_casing: Option<bool>,
    pub space_around_operators: Option<bool>,
    pub space_around_pipe: Option<bool>,
    pub space_after_separator: Option<bool>,
}

pub fn generate_schema_value() -> Result<Value, serde_json::Error> {
    let schema: Schema = SchemaSettings::draft07()
        .into_generator()
        .into_root_schema_for::<DprintPowerShellConfigSchema>();
    let mut value = serde_json::to_value(schema)?;
    let object = value
        .as_object_mut()
        .expect("generated schema should be an object");
    object.insert(
        "$schema".to_string(),
        json!("http://json-schema.org/draft-07/schema#"),
    );
    object.insert("$id".to_string(), json!(SCHEMA_URL));
    Ok(json_schema_sort::sorted_schema(value))
}
