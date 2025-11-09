namespace Rca.Tools.MetricsReporter.Rendering;

/// <summary>
/// Generates CSS styles for the HTML metrics report.
/// </summary>
internal static class HtmlStylesGenerator
{
    /// <summary>
    /// Generates the complete CSS stylesheet for the metrics report.
    /// </summary>
    /// <returns>The CSS stylesheet as a string.</returns>
    public static string Generate()
        => @"
:root {
  color-scheme: light dark;
  font-family: sans-serif;
  font-size: 0.9em;
  /* Border variables for consistent styling */
  --border-dark-color:rgb(175, 175, 175);
  --border-dark-width: 1.5px;
  --border-light-color:rgb(190, 190, 190);
  --border-light-width: 1px;
}
html {
  background-color:rgb(235, 235, 235);
}
body {
  margin: 12px;
}
/* Add side margins for wide screens (width > 1200px) */
@media (min-width: 1201px) {
  body {
    margin-top: 12px;
    margin-bottom: 12px;
    margin-left: 110px;
    margin-right: 110px;
  }
}
h1 { margin-bottom: 4px; }
.meta p { margin: 2px 0; }
.legend { margin: 8px 0 12px; display:flex; gap:8px; }
.badge { padding:2px 6px; border-radius:4px; font-size:11px; font-weight:600; text-transform:uppercase; }
.badge-new { background:#1a7f37; color:#fff; font-size:9px; }
.status-warning{ background:rgb(255,235,156); color:#b66f1a; font-size:9px; padding:3px 6px; }
.status-error{ background:rgb(255,200,200); color:#d9534f; font-size:9px; padding:3px 6px; }
.table-actions{ display:flex; align-items:center; justify-content:flex-end; gap:8px; margin:0; position:sticky; top:0; background:rgb(235, 235, 235); z-index:10; padding:6px 0 }
.status-badges{ display:flex; gap:8px; align-items:center; }
.table-actions button{ margin-left:6px; padding:6px 10px; font-size:12px }
.table-container{ max-width:100%; }
.metrics{ border-collapse:collapse; width:100%; table-layout:fixed; word-wrap:break-word; border-spacing:0; font-size:0.9em; border:var(--border-dark-width) solid var(--border-dark-color); }
/* Clean borders - each cell has only right and bottom borders to prevent double borders */
.metrics th, .metrics td{ border-right:var(--border-light-width) solid var(--border-light-color); border-bottom:var(--border-light-width) solid var(--border-light-color); border-top:none; border-left:none; padding:1px 3px; vertical-align:middle; line-height:1.3; height:auto; -webkit-hyphens:auto; -ms-hyphens:auto; hyphens:auto; }
.metrics th:first-child, .metrics td:first-child{ border-left:var(--border-dark-width) solid var(--border-dark-color); }
.metrics th:last-child, .metrics td:last-child{ border-right:var(--border-dark-width) solid var(--border-dark-color); }
/* Group separator borders - darker borders to separate column groups */
/* Symbol column (first column) - right border darker, bottom border darker for header cell */
.metrics th[data-col='symbol'], .metrics td.symbol, .metrics th.symbol{ border-right:var(--border-dark-width) solid var(--border-dark-color); }
.metrics thead th[data-col='symbol']{ border-bottom:var(--border-dark-width) solid var(--border-dark-color) !important; }
/* AltCover group - last column (AltCoverCyclomaticComplexity) has darker right border, applies to both header rows */
.metrics th[data-col='AltCoverCyclomaticComplexity'], .metrics td.metric[data-col='AltCoverCyclomaticComplexity'], .metrics th.metric[data-col='AltCoverCyclomaticComplexity']{ border-right:var(--border-dark-width) solid var(--border-dark-color); }
.metrics th[data-col-group='AltCover']{ border-right:var(--border-dark-width) solid var(--border-dark-color); }
/* Roslyn group - first column (RoslynCyclomaticComplexity) has darker left border, last column (RoslynExecutableLines) has darker right border */
.metrics th[data-col='RoslynCyclomaticComplexity'], .metrics td.metric[data-col='RoslynCyclomaticComplexity'], .metrics th.metric[data-col='RoslynCyclomaticComplexity']{ border-left:var(--border-dark-width) solid var(--border-dark-color); }
.metrics th[data-col='RoslynExecutableLines'], .metrics td.metric[data-col='RoslynExecutableLines'], .metrics th.metric[data-col='RoslynExecutableLines']{ border-right:var(--border-dark-width) solid var(--border-dark-color); }
.metrics th[data-col-group='Roslyn']{ border-left:var(--border-dark-width) solid var(--border-dark-color); border-right:var(--border-dark-width) solid var(--border-dark-color); }
/* Sarif group - first column (SarifCaRuleViolations) has darker left border */
.metrics th[data-col='SarifCaRuleViolations'], .metrics td.metric[data-col='SarifCaRuleViolations'], .metrics th.metric[data-col='SarifCaRuleViolations']{ border-left:var(--border-dark-width) solid var(--border-dark-color); }
.metrics th[data-col-group='Sarif']{ border-left:var(--border-dark-width) solid var(--border-dark-color); }
/* Header separation - darker border between header and body */
.metrics thead th{ border-top:var(--border-dark-width) solid var(--border-dark-color); }
.metrics thead tr:last-child th{ border-bottom:var(--border-dark-width) solid var(--border-dark-color) !important; }
.metrics tbody tr:first-child td, .metrics tbody tr:first-child th{ border-top:var(--border-dark-width) solid var(--border-dark-color) !important; }
/* Bottom border of table - darker border for last row */
.metrics tbody tr:last-child td, .metrics tbody tr:last-child th{ border-bottom:var(--border-dark-width) solid var(--border-dark-color); }
/* Table headers - sticky header rows, positioned below table-actions */
.metrics thead th{ background-color:#d1d1d1; text-align:left; white-space:normal; word-wrap:break-word; cursor:pointer; position:sticky; top:40px; z-index:5; will-change:transform; -webkit-hyphens:auto; -ms-hyphens:auto; hyphens:auto; }
/* First header row: group labels (AltCover, Roslyn, Sarif) - center aligned, increased height, top border using box-shadow */
.metrics thead tr:first-child th{ padding-top:8px; padding-bottom:8px; box-shadow:0 calc(-1 * var(--border-dark-width)) 0 0 var(--border-dark-color); }
.metrics thead tr:first-child th:not([data-col='symbol']){ text-align:center; font-weight:bold; }
/* Second header row: top border using box-shadow to separate from first row */
.metrics thead tr:nth-child(2) th{ box-shadow:0 calc(-1 * var(--border-light-width)) 0 0 var(--border-light-color); }
/* Node rows (with expander) - use th, gray background, bold black text */
.metrics tr.node-header th{ background-color:#ddd; font-weight:bold; color:#000; text-align:left; white-space:nowrap; overflow:hidden; text-overflow:ellipsis }
.metrics tr.node-header th:first-child{ background-color:#dcdcdc; }
/* Regular rows (items) - use td, striped background, red text in first column */
.metrics tr.node-item td{ text-align:left; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; background-color:#fff; }
/* Striped rows - alternate white and gray for item rows only (handled by JS classes) */
.metrics tr.node-item.stripe-odd td{ background-color:#F3F3F3; }
.metrics tr.node-item.stripe-even td{ background-color:#fff; }
/* Red text for item names in first column */
.metrics tr.node-item td.symbol .item-name{ color:#c00; }
/* Fixed width for first column */
th[data-col='symbol'], td.symbol, th.symbol { width:420px; box-sizing:border-box }
.symbol{ position:relative; width:420px; white-space:nowrap; overflow:hidden; box-sizing:border-box; line-height:inherit; }
/* Equal width for all other columns */
.metrics thead th:not([data-col='symbol']), .metrics td.metric, .metrics th.metric { width:auto }
.symbol .name-text{ display:inline-block; vertical-align:middle; max-width:100%; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
/* Cell backgrounds based on status - success uses base row color, only warning/error have colored backgrounds */
/* For node-header rows (assembly, namespace, class) - darker colors to contrast with gray background #ddd */
.metrics tr.node-header th.metric[data-status='warning']{ background: rgba(255,235,156,0.3) }
.metrics tr.node-header th.metric[data-status='error']{ background: rgba(255,200,200,0.25) }
/* For node-item rows (members) - lighter colors to contrast with white/light-gray background */
.metrics tr.node-item td.metric[data-status='warning']{ background: rgba(255,248,220,0.6) }
.metrics tr.node-item td.metric[data-status='error']{ background: rgba(255,240,240,0.5) }
/* Text colors - black for success, colored for warning/error */
.metrics tr.node-item td.metric[data-status='error'] .metric-value{ color:#d9534f }
.metrics tr.node-item td.metric[data-status='warning'] .metric-value{ color:#b66f1a }
.metrics tr.node-header th.metric[data-status='error'] .metric-value{ color:#d9534f }
.metrics tr.node-header th.metric[data-status='warning'] .metric-value{ color:#b66f1a }
/* Metric values - bold for node-header rows, normal for node-item rows */
.metrics tr.node-header th .metric-value{ font-weight:bold }
.metrics tr.node-item td .metric-value{ font-weight:normal }
.metric-value{ font-weight:normal }
.delta-positive{ color:#1a7f37; margin-left:4px }
.delta-negative{ color:#d9534f; margin-left:4px }
.fqn{ font-family:'Consolas','Courier New',monospace; font-size:12px; color:rgba(128,128,128,0.8) }
/* expander button - fixed positioning to avoid text overlap */
.expander{ position:absolute; left:0; top:50%; transform:translateY(-50%); border:0; background:transparent; cursor:pointer; font-size:14px; line-height:1; width:20px; height:20px; display:flex; align-items:center; justify-content:center; z-index:1; padding:0; margin:0; user-select:none; font-weight:bold; }
.expander:focus{ outline:1px solid rgba(0,0,0,0.3); outline-offset:2px; border-radius:2px }
/* padding-left for symbol cells: level-based indentation */
/* Base: no indentation for level 0 */
tr.node-row[data-level='0'] .symbol{ padding-left:0 }
tr.node-row[data-level='0'] .symbol.has-expander{ padding-left:20px }
/* Levels 1-4: indentation increases by 12px per level */
/* Level 1: 24px base (20px for expander area + 4px spacing) */
tr.node-row[data-level='1'] .symbol{ padding-left:24px }
tr.node-row[data-level='1'] .symbol:not(.has-expander){ padding-left:12px }
/* Level 2: 36px base */
tr.node-row[data-level='2'] .symbol{ padding-left:36px }
tr.node-row[data-level='2'] .symbol:not(.has-expander){ padding-left:24px }
/* Level 3: 48px base */
tr.node-row[data-level='3'] .symbol{ padding-left:48px }
tr.node-row[data-level='3'] .symbol:not(.has-expander){ padding-left:36px }
/* Level 4: 60px base */
tr.node-row[data-level='4'] .symbol{ padding-left:60px }
tr.node-row[data-level='4'] .symbol:not(.has-expander){ padding-left:48px }
/* Ensure text doesn't overlap with expander */
.symbol .name-text{ margin-left:0; min-width:0 }
/* Hover effect for rows */
.metrics tbody tr.node-item:hover td{ background-color:#b0b0b0; }
.metrics tbody tr.node-header:hover th{ background-color:#c1c1c1; }
";
}

