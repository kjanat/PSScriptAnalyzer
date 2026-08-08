mod formatter;

#[cfg(feature = "schema")]
pub mod schema;

use anyhow::anyhow;
use dprint_core::configuration::{
    ConfigKeyMap, ConfigurationDiagnostic, GlobalConfiguration, get_unknown_property_diagnostics,
    get_value,
};
use dprint_core::plugins::{
    CheckConfigUpdatesMessage, ConfigChange, FileMatchingInfo, FormatResult, PluginInfo,
    PluginResolveConfigurationResult, SyncFormatRequest, SyncHostFormatRequest, SyncPluginHandler,
};
use serde::{Deserialize, Serialize};

pub const SCHEMA_URL: &str = concat!(
    "https://github.com/kjanat/PSScriptAnalyzer/releases/download/dprint-powershell-",
    env!("CARGO_PKG_VERSION"),
    "/schema.json"
);
pub const UPDATE_URL: &str = "https://github.com/kjanat/PSScriptAnalyzer/releases/latest/download/dprint-powershell-latest.json";

#[derive(Clone, Copy, Debug, Default, Deserialize, Eq, PartialEq, Serialize)]
#[cfg_attr(feature = "schema", derive(schemars::JsonSchema))]
#[serde(rename_all = "camelCase")]
pub enum BraceStyle {
    #[default]
    SameLine,
    NextLine,
}
dprint_core::generate_str_to_from![BraceStyle, [SameLine, "sameLine"], [NextLine, "nextLine"]];

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Configuration {
    pub brace_style: BraceStyle,
    pub indent_width: u8,
    pub use_tabs: bool,
    pub correct_keyword_casing: bool,
    pub space_around_operators: bool,
    pub space_around_pipe: bool,
    pub space_after_separator: bool,
}

pub struct PowerShellPluginHandler;

impl SyncPluginHandler<Configuration> for PowerShellPluginHandler {
    fn plugin_info(&mut self) -> PluginInfo {
        PluginInfo {
            name: env!("CARGO_PKG_NAME").to_string(),
            version: env!("CARGO_PKG_VERSION").to_string(),
            config_key: "powerShell".to_string(),
            help_url: env!("CARGO_PKG_REPOSITORY").to_string(),
            config_schema_url: SCHEMA_URL.to_string(),
            update_url: Some(UPDATE_URL.to_string()),
        }
    }

    fn license_text(&mut self) -> String {
        include_str!("../../LICENSE").to_string()
    }

    fn resolve_config(
        &mut self,
        mut config: ConfigKeyMap,
        global_config: &GlobalConfiguration,
    ) -> PluginResolveConfigurationResult<Configuration> {
        let mut diagnostics = Vec::<ConfigurationDiagnostic>::new();
        let brace_style = get_value(
            &mut config,
            "braceStyle",
            BraceStyle::default(),
            &mut diagnostics,
        );
        let mut indent_width = get_value(
            &mut config,
            "indentWidth",
            global_config.indent_width.unwrap_or(4),
            &mut diagnostics,
        );
        let use_tabs = get_value(
            &mut config,
            "useTabs",
            global_config.use_tabs.unwrap_or(false),
            &mut diagnostics,
        );
        let correct_keyword_casing =
            get_value(&mut config, "correctKeywordCasing", true, &mut diagnostics);
        let space_around_operators =
            get_value(&mut config, "spaceAroundOperators", true, &mut diagnostics);
        let space_around_pipe = get_value(&mut config, "spaceAroundPipe", true, &mut diagnostics);
        let space_after_separator =
            get_value(&mut config, "spaceAfterSeparator", true, &mut diagnostics);

        if indent_width > 32 {
            diagnostics.push(ConfigurationDiagnostic {
                property_name: "indentWidth".to_string(),
                message: "Expected a value from 0 through 32.".to_string(),
            });
            indent_width = 4;
        }

        diagnostics.extend(get_unknown_property_diagnostics(config));

        PluginResolveConfigurationResult {
            file_matching: FileMatchingInfo {
                file_extensions: vec!["ps1".into(), "psm1".into(), "psd1".into()],
                file_names: Vec::new(),
            },
            diagnostics,
            config: Configuration {
                brace_style,
                indent_width,
                use_tabs,
                correct_keyword_casing,
                space_around_operators,
                space_around_pipe,
                space_after_separator,
            },
        }
    }

    fn check_config_updates(
        &self,
        _message: CheckConfigUpdatesMessage,
    ) -> anyhow::Result<Vec<ConfigChange>> {
        Ok(Vec::new())
    }

    fn format(
        &mut self,
        request: SyncFormatRequest<Configuration>,
        _format_with_host: impl FnMut(SyncHostFormatRequest) -> FormatResult,
    ) -> FormatResult {
        if request.range.is_some() || request.token.is_cancelled() {
            return Ok(None);
        }

        let source = std::str::from_utf8(&request.file_bytes)
            .map_err(|error| anyhow!("file is not valid UTF-8: {error}"))?;
        let formatted = formatter::format(source, request.config)?;
        if formatted == source || request.token.is_cancelled() {
            Ok(None)
        } else {
            Ok(Some(formatted.into_bytes()))
        }
    }
}

#[cfg(all(target_arch = "wasm32", target_os = "unknown"))]
dprint_core::generate_plugin_code!(
    PowerShellPluginHandler,
    PowerShellPluginHandler,
    Configuration
);
