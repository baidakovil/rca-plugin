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
}
html {
  background-color: #d6d6d6;
}
body {
  margin: 12px;
}
h1 { margin-bottom: 4px; }
.meta p { margin: 2px 0; }
.legend { margin: 8px 0 12px; display:flex; gap:8px; }
.badge { padding:2px 6px; border-radius:4px; font-size:11px; font-weight:600; text-transform:uppercase; }
.badge-new { background:#1a7f37; color:#fff }
.status-success{ background:#1a7f37; color:#fff }
.status-warning{ background:#f0ad4e; color:#000 }
.status-error{ background:#d9534f; color:#fff }
.status-notapplicable, .status-na{ background:#6c757d; color:#fff }
.table-actions{ display:flex; align-items:center; justify-content:flex-end; gap:8px; margin:8px 0 10px; position:sticky; top:8px; background:linear-gradient(transparent, rgba(255,255,255,0.6)); z-index:3; padding:6px 0 }
.table-actions button{ margin-left:6px; padding:6px 10px; font-size:12px }
.table-container{ overflow:auto; max-width:100%; }
.metrics{ border:1px solid #c1c1c1; border-collapse:collapse; width:100%; table-layout:auto; word-wrap:break-word; }
/* Table headers - sticky header row */
.metrics thead th{ border:1px solid #c1c1c1; padding:2px 4px 2px 4px; background-color:#d1d1d1; text-align:left; vertical-align:top; white-space:normal; word-break:break-word; cursor:pointer; position:sticky; top:0 }
/* Node rows (with expander) - use th, gray background, bold black text */
.metrics tr.node-header th{ border:1px solid #c1c1c1; padding:2px 4px 2px 4px; background-color:#ddd; font-weight:bold; color:#000; text-align:left; vertical-align:top; white-space:normal; word-break:break-word }
.metrics tr.node-header th:first-child{ background-color:#dcdcdc; }
/* Regular rows (items) - use td, striped background, red text in first column */
.metrics tr.node-item td{ border:1px solid #c1c1c1; padding:2px 5px 2px 5px; text-align:left; vertical-align:top; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; background-color:#fff; }
/* Striped rows - alternate white and gray for item rows only (handled by JS classes) */
.metrics tr.node-item.stripe-odd td{ background-color:#F3F3F3; }
.metrics tr.node-item.stripe-even td{ background-color:#fff; }
/* Red text for item names in first column */
.metrics tr.node-item td.symbol .item-name{ color:#c00; }
/* reasonable default width for first column; allow other columns to remain visible */
th[data-col='symbol'], td.symbol, th.symbol { width:420px; min-width:240px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; box-sizing:border-box }
.symbol{ position:relative; width:420px; min-width:240px; white-space:nowrap; overflow:hidden; box-sizing:border-box; display:flex; align-items:center }
.symbol .name-text{ display:inline-block; vertical-align:middle; max-width:100%; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; flex:1 }
/* soft translucent cell backgrounds based on status - only for regular rows */
.metrics tr.node-item td.metric[data-status='success']{ background: rgba(26,127,55,0.12) }
.metrics tr.node-item td.metric[data-status='warning']{ background: rgba(240,173,78,0.12) }
.metrics tr.node-item td.metric[data-status='error']{ background: rgba(217,83,79,0.12) }
.metrics tr.node-item td.metric[data-status='na'], .metrics tr.node-item td.metric[data-status='notapplicable']{ background: rgba(108,117,125,0.06) }
.metrics tr.node-item td.metric[data-status='error'] .metric-value{ color:#d9534f }
.metrics tr.node-item td.metric[data-status='warning'] .metric-value{ color:#b66f1a }
.metrics tr.node-item td.metric[data-status='success'] .metric-value{ color:#1a7f37 }
.metric-value{ font-weight:600 }
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

