namespace Rca.Tools.MetricsReporter.Rendering.Scripts;

using System;
using System.Collections.Generic;

/// <summary>
/// Provides legacy JavaScript modules used during the refactor transition.
/// </summary>
internal static partial class JavascriptModules
{
  private static IReadOnlyCollection<ScriptFragment>? _legacyFragments;

  /// <summary>
  /// Gets the legacy monolithic script used by <see cref="HtmlScriptGenerator"/>.
  /// </summary>
  internal static string Legacy => ScriptComposer.Compose(LegacyFragments);

  /// <summary>
  /// Gets the carved fragments of the legacy script.
  /// </summary>
  internal static IReadOnlyCollection<ScriptFragment> LegacyFragments
    => _legacyFragments ??= BuildLegacyFragments();

  private static IReadOnlyCollection<ScriptFragment> BuildLegacyFragments()
  {
    var segments = new (string Name, string? Marker)[]
    {
      ("LegacyPrelude", "// Suppressed metrics tooltip functionality"),
      ("LegacySuppressionTooltip", "// Symbol tooltip functionality"),
      ("LegacySymbolTooltip", "  var state = {"),
      ("LegacyStateModule", "  // Filter functionality"),
      ("LegacyFilterModule", "  // Meta section spoiler toggle"),
      ("LegacyFinalModule", null)
    };

    var fragments = new List<ScriptFragment>(segments.Length);
    var start = 0;

    foreach(var (name, marker) in segments)
    {
      if(marker is null)
      {
        var tail = LegacyBlob.Substring(start);
        fragments.Add(new ScriptFragment(name, tail));
        break;
      }

      var index = LegacyBlob.IndexOf(marker, start, StringComparison.Ordinal);
      if(index < 0)
      {
        throw new InvalidOperationException($"Marker '{marker}' not found when carving legacy script.");
      }

      var content = LegacyBlob.Substring(start, index - start);
      fragments.Add(new ScriptFragment(name, content));
      start = index;
    }

    return fragments;
  }

  /// <summary>
  /// Original monolithic script body.
  /// </summary>
  private const string LegacyBlob = @"(function(){
  var table = document.getElementById('metrics-table');
  if(!table) return;

  var tbody = table.tBodies[0];
  if(!tbody) return;

  var thresholdElement = document.getElementById('threshold-data');
  var thresholdData = null;
  if(thresholdElement){
    try{
      var rawText = thresholdElement.textContent || thresholdElement.innerText || '';
      if(rawText.trim().length > 0){
        thresholdData = JSON.parse(rawText);
      }
    }catch(_){
      thresholdData = null;
    }
  }

  var hasThresholdData = thresholdData && Object.keys(thresholdData).length > 0;
  var tooltip = null;
  var suppressionTooltip = null;
  var tooltipTimer = null;
  var tooltipVisible = false;
  var tooltipTarget = null;
  var suppressionTooltipTimer = null;
  var suppressionTooltipVisible = false;
  var suppressionTooltipTarget = null;

  var levelOrder = ['Solution', 'Assembly', 'Namespace', 'Type', 'Member'];
  var levelLabels = {
    Solution: 'Solution',
    Assembly: 'Assembly',
    Namespace: 'Namespace',
    Type: 'Type',
    Member: 'Member'
  };

  if(hasThresholdData){
    tooltip = document.createElement('div');
    tooltip.className = 'metric-tooltip';
    tooltip.style.display = 'none';
    document.body.appendChild(tooltip);
  } else {
    thresholdData = null;
  }

  suppressionTooltip = document.createElement('div');
  suppressionTooltip.className = 'metric-tooltip';
  suppressionTooltip.style.display = 'none';
  document.body.appendChild(suppressionTooltip);

  var symbolTooltip = document.createElement('div');
  symbolTooltip.className = 'metric-tooltip';
  symbolTooltip.style.display = 'none';
  document.body.appendChild(symbolTooltip);
  var symbolTooltipTimer = null;
  var symbolTooltipVisible = false;
  var symbolTooltipTarget = null;

  var simpleTooltip = document.createElement('div');
  simpleTooltip.className = 'metric-tooltip';
  simpleTooltip.style.display = 'none';
  document.body.appendChild(simpleTooltip);
  var simpleTooltipTimer = null;
  var simpleTooltipVisible = false;
  var simpleTooltipTarget = null;

  var preferencesStorageBaseKey = 'rcaMetricsReport.preferences';
  var preferencesLocationKey = (typeof window !== 'undefined' && window.location && window.location.pathname)
    ? window.location.pathname
    : 'report';
  var preferencesStorageKey = preferencesStorageBaseKey + ':' + preferencesLocationKey;
  function readPreferences(){
    try{
      if(typeof window === 'undefined' || typeof window.localStorage === 'undefined'){
        return null;
      }
      var serialized = window.localStorage.getItem(preferencesStorageKey);
      if(!serialized){
        return null;
      }
      return JSON.parse(serialized);
    }catch(_){
      return null;
    }
  }
  function writePreferences(value){
    if(!value){
      return;
    }
    try{
      if(typeof window === 'undefined' || typeof window.localStorage === 'undefined'){
        return;
      }
      window.localStorage.setItem(preferencesStorageKey, JSON.stringify(value));
    }catch(_){
      // Ignore storage errors (e.g. private mode)
    }
  }
  var savedPreferences = readPreferences();
  var isRestoringPreferences = savedPreferences !== null;

  function escapeHtml(value){
    if(value === null || value === undefined){
      return '';
    }
    return String(value)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/""/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function copyTextToClipboard(value){
    if(!value){
      return;
    }
    try{
      if(navigator && navigator.clipboard && navigator.clipboard.writeText){
        navigator.clipboard.writeText(value);
        return;
      }
    }catch(_){
      // ignore clipboard access errors
    }
    var textarea = document.createElement('textarea');
    textarea.value = value;
    textarea.setAttribute('readonly', '');
    textarea.style.position = 'absolute';
    textarea.style.left = '-9999px';
    document.body.appendChild(textarea);
    textarea.select();
    try{
      document.execCommand('copy');
    }catch(_){
      // ignore failures
    }
    document.body.removeChild(textarea);
  }

  function formatThresholdValue(value){
    if(value === null || value === undefined){
      return '—';
    }
    return String(value);
  }

  function cancelTooltipTimer(){
    if(tooltipTimer){
      clearTimeout(tooltipTimer);
      tooltipTimer = null;
    }
  }

  function hideTooltip(){
    cancelTooltipTimer();
    if(!tooltipVisible || !tooltip){
      tooltipTarget = null;
      return;
    }
    tooltip.style.display = 'none';
    tooltipVisible = false;
    tooltipTarget = null;
  }

  function positionTooltip(target, tooltipElement){
    var actualTooltip = tooltipElement || tooltip;
    if(!actualTooltip || !target){
      return;
    }
    var rect = target.getBoundingClientRect();
    var tooltipRect = actualTooltip.getBoundingClientRect();
    
    // WHY: For buttons (C, F), position tooltip below with more offset to avoid cursor blocking
    // Check if target is a button with row-action-icon class or data-action attribute
    var isButton = target.classList && target.classList.contains('row-action-icon');
    
    // WHY: Larger offset for buttons (24px) to ensure tooltip appears well below cursor/pointer
    // This prevents the cursor from covering the tooltip text
    var top = window.scrollY + rect.bottom + (isButton ? 24 : 8);
    var left = window.scrollX + rect.left + (rect.width - tooltipRect.width) / 2;

    var viewportRight = window.scrollX + window.innerWidth;
    var viewportBottom = window.scrollY + window.innerHeight;

    if(left + tooltipRect.width > viewportRight - 8){
      left = viewportRight - tooltipRect.width - 8;
    }
    if(left < window.scrollX + 8){
      left = window.scrollX + 8;
    }
    
    // WHY: For buttons, always position below to avoid cursor blocking, even if it goes off-screen
    // For other elements, allow positioning above if needed
    if(!isButton && top + tooltipRect.height > viewportBottom - 8){
      top = window.scrollY + rect.top - tooltipRect.height - 8;
    }

    actualTooltip.style.left = left + 'px';
    actualTooltip.style.top = top + 'px';
  }

  function buildTooltipHtml(metricId){
    if(!thresholdData || !thresholdData[metricId]){
      return null;
    }
    var info = thresholdData[metricId];
    var parts = [];
    if(info.description){
      parts.push('<p class=""metric-tooltip__desc""><em>' + escapeHtml(info.description) + '</em></p>');
    }
    var direction = info.higherIsBetter ? 'Higher values are better' : 'Lower values are better';
    parts.push('<p class=""metric-tooltip__direction""><em>' + escapeHtml(direction) + '</em></p>');
    parts.push('<p class=""metric-tooltip__heading""><strong>Warning / Error</strong></p>');
    parts.push('<ul class=""metric-tooltip__list"">');
    var levels = info.levels || {};
    for(var i = 0; i < levelOrder.length; i++){
      var levelKey = levelOrder[i];
      var label = levelLabels[levelKey] || levelKey;
      var entry = levels[levelKey];
      var warning = entry && entry.warning !== undefined ? entry.warning : null;
      var error = entry && entry.error !== undefined ? entry.error : null;
      parts.push('<li><strong>' + escapeHtml(label) + ':</strong> <span>' + escapeHtml(formatThresholdValue(warning)) + ' / ' + escapeHtml(formatThresholdValue(error)) + '</span></li>');
    }
    parts.push('</ul>');
    return parts.join('');
  }

  function showTooltip(target){
    if(!tooltip || !thresholdData){
      return;
    }
    var metricId = target && target.dataset ? target.dataset.metricId : null;
    if(!metricId || !thresholdData[metricId]){
      return;
    }
    var html = buildTooltipHtml(metricId);
    if(!html){
      return;
    }
    tooltip.innerHTML = html;
    tooltip.style.display = 'block';
    tooltipVisible = true;
    tooltipTarget = target;
    positionTooltip(target);
  }

  function scheduleTooltip(target){
    if(!hasThresholdData || !target){
      return;
    }
    cancelTooltipTimer();
    tooltipTimer = setTimeout(function(){
      showTooltip(target);
    }, 500);
  }

  function handleHeaderMouseOver(event){
    var th = event.target.closest('th[data-metric-id]');
    if(!th){
      return;
    }
    var metricId = th.dataset.metricId;
    if(!metricId || metricId === 'symbol'){
      return;
    }
    if(tooltipTarget && tooltipTarget !== th){
      hideTooltip();
    }
    scheduleTooltip(th);
  }

  function handleHeaderMouseOut(event){
    var th = event.target.closest('th[data-metric-id]');
    if(!th){
      return;
    }
    var related = event.relatedTarget;
    if(related && th.contains(related)){
      return;
    }
    cancelTooltipTimer();
    hideTooltip();
  }

  if(hasThresholdData && table && table.tHead){
    table.tHead.addEventListener(""mouseover"", handleHeaderMouseOver);
    table.tHead.addEventListener(""mouseout"", handleHeaderMouseOut);
    table.tHead.addEventListener(""focusin"", function(e){
      var th = e.target.closest('th[data-metric-id]');
      if(!th){
        return;
      }
      if(th.dataset && th.dataset.metricId === 'symbol'){
        return;
      }
      scheduleTooltip(th);
    }, true);
    table.tHead.addEventListener(""focusout"", function(e){
      var th = e.target.closest('th[data-metric-id]');
      if(!th){
        return;
      }
      hideTooltip();
    }, true);
  }

  if(hasThresholdData){
    window.addEventListener(""scroll"", function(){
      if(tooltipVisible && tooltipTarget){
        positionTooltip(tooltipTarget);
      }
      if(suppressionTooltipVisible && suppressionTooltipTarget){
        positionTooltip(suppressionTooltipTarget, suppressionTooltip);
      }
      if(symbolTooltipVisible && symbolTooltipTarget){
        positionTooltip(symbolTooltipTarget, symbolTooltip);
      }
      if(simpleTooltipVisible && simpleTooltipTarget){
        positionTooltip(simpleTooltipTarget, simpleTooltip);
      }
    }, true);
    window.addEventListener(""resize"", function(){
      if(tooltipVisible && tooltipTarget){
        positionTooltip(tooltipTarget);
      }
      if(suppressionTooltipVisible && suppressionTooltipTarget){
        positionTooltip(suppressionTooltipTarget, suppressionTooltip);
      }
      if(symbolTooltipVisible && symbolTooltipTarget){
        positionTooltip(symbolTooltipTarget, symbolTooltip);
      }
      if(simpleTooltipVisible && simpleTooltipTarget){
        positionTooltip(simpleTooltipTarget, simpleTooltip);
      }
    });
  } else {
    window.addEventListener(""scroll"", function(){
      if(suppressionTooltipVisible && suppressionTooltipTarget){
        positionTooltip(suppressionTooltipTarget, suppressionTooltip);
      }
      if(symbolTooltipVisible && symbolTooltipTarget){
        positionTooltip(symbolTooltipTarget, symbolTooltip);
      }
      if(simpleTooltipVisible && simpleTooltipTarget){
        positionTooltip(simpleTooltipTarget, simpleTooltip);
      }
    }, true);
    window.addEventListener(""resize"", function(){
      if(suppressionTooltipVisible && suppressionTooltipTarget){
        positionTooltip(suppressionTooltipTarget, suppressionTooltip);
      }
      if(symbolTooltipVisible && symbolTooltipTarget){
        positionTooltip(symbolTooltipTarget, symbolTooltip);
      }
      if(simpleTooltipVisible && simpleTooltipTarget){
        positionTooltip(simpleTooltipTarget, simpleTooltip);
      }
    });
  }

  // Suppressed metrics tooltip functionality
  function cancelSuppressionTooltipTimer(){
    if(suppressionTooltipTimer){
      clearTimeout(suppressionTooltipTimer);
      suppressionTooltipTimer = null;
    }
  }

  function hideSuppressionTooltip(){
    cancelSuppressionTooltipTimer();
    if(!suppressionTooltipVisible || !suppressionTooltip){
      suppressionTooltipTarget = null;
      return;
    }
    suppressionTooltip.style.display = 'none';
    suppressionTooltipVisible = false;
    suppressionTooltipTarget = null;
  }

  function buildSuppressionTooltipHtml(suppressionInfo){
    if(!suppressionInfo){
      return null;
    }
    var parts = [];
    parts.push('<p class=""metric-tooltip__heading""><strong>Suppressed ' + escapeHtml(suppressionInfo.ruleId || '') + '</strong></p>');
    if(suppressionInfo.justification){
      // WHY: Justification text already contains HTML formatting (bold tags, line breaks)
      // from FormatJustificationText, so we use innerHTML-safe approach.
      parts.push('<p class=""metric-tooltip__desc"">' + suppressionInfo.justification + '</p>');
    } else {
      parts.push('<p class=""metric-tooltip__desc"">Suppressed via SuppressMessage.</p>');
    }
    return parts.join('');
  }

  function showSuppressionTooltip(target){
    if(!suppressionTooltip || !target){
      return;
    }
    var suppressionData = target.dataset.suppressionInfo;
    if(!suppressionData){
      return;
    }
    try{
      var suppressionInfo = JSON.parse(suppressionData);
      var html = buildSuppressionTooltipHtml(suppressionInfo);
      if(!html){
        return;
      }
      suppressionTooltip.innerHTML = html;
      suppressionTooltip.style.display = 'block';
      suppressionTooltipVisible = true;
      suppressionTooltipTarget = target;
      positionTooltip(target, suppressionTooltip);
    }catch(_){
      // Invalid JSON, ignore
    }
  }

  function scheduleSuppressionTooltip(target){
    if(!target){
      return;
    }
    cancelSuppressionTooltipTimer();
    suppressionTooltipTimer = setTimeout(function(){
      showSuppressionTooltip(target);
    }, 500);
  }

  // Suppressed tooltip handling is now in unified handler

  // Symbol tooltip functionality
  function cancelSymbolTooltipTimer(){
    if(symbolTooltipTimer){
      clearTimeout(symbolTooltipTimer);
      symbolTooltipTimer = null;
    }
  }

  function hideSymbolTooltip(){
    cancelSymbolTooltipTimer();
    if(!symbolTooltipVisible || !symbolTooltip){
      symbolTooltipTarget = null;
      return;
    }
    symbolTooltip.style.display = 'none';
    symbolTooltipVisible = false;
    symbolTooltipTarget = null;
  }

  function buildSymbolTooltipHtml(symbolInfo){
    if(!symbolInfo){
      return null;
    }
    var parts = [];
    var role = symbolInfo.role || '';
    var fqn = symbolInfo.fullyQualifiedName || '';
    parts.push('<p class=""metric-tooltip__heading""><strong>' + escapeHtml(role) + '</strong></p>');
    if(fqn){
      parts.push('<p class=""metric-tooltip__desc"">' + escapeHtml(fqn) + '</p>');
    }
    if(symbolInfo.sourcePath){
      var label = symbolInfo.sourcePath;
      var hasStart = typeof symbolInfo.sourceStartLine === 'number';
      var hasEnd = typeof symbolInfo.sourceEndLine === 'number';
      if(hasStart){
        label += ':' + symbolInfo.sourceStartLine;
        if(hasEnd && symbolInfo.sourceEndLine !== symbolInfo.sourceStartLine){
          label += '-' + symbolInfo.sourceEndLine;
        }
      }
      parts.push('<p class=""metric-tooltip__desc""><strong>Path:</strong> <code>' + escapeHtml(label) + '</code></p>');
    }
    return parts.join('');
  }

  function showSymbolTooltip(target){
    if(!symbolTooltip || !target){
      return;
    }
    var symbolData = target.dataset.symbolInfo;
    if(!symbolData){
      return;
    }
    try{
      var symbolInfo = JSON.parse(symbolData);
      var html = buildSymbolTooltipHtml(symbolInfo);
      if(!html){
        return;
      }
      symbolTooltip.innerHTML = html;
      symbolTooltip.style.display = 'block';
      symbolTooltipVisible = true;
      symbolTooltipTarget = target;
      positionTooltip(target, symbolTooltip);
    }catch(_){
      // Invalid JSON, ignore
    }
  }

  function scheduleSymbolTooltip(target){
    if(!target){
      return;
    }
    cancelSymbolTooltipTimer();
    symbolTooltipTimer = setTimeout(function(){
      showSymbolTooltip(target);
    }, 500);
  }


  // Simple tooltip functionality
  function cancelSimpleTooltipTimer(){
    if(simpleTooltipTimer){
      clearTimeout(simpleTooltipTimer);
      simpleTooltipTimer = null;
    }
  }

  function hideSimpleTooltip(){
    cancelSimpleTooltipTimer();
    if(!simpleTooltipVisible || !simpleTooltip){
      simpleTooltipTarget = null;
      return;
    }
    simpleTooltip.style.display = 'none';
    simpleTooltipVisible = false;
    simpleTooltipTarget = null;
  }

  function buildSimpleTooltipHtml(text){
    if(!text){
      return null;
    }
    return '<p class=""metric-tooltip__desc"">' + escapeHtml(text) + '</p>';
  }

  function showSimpleTooltip(target){
    if(!simpleTooltip || !target){
      return;
    }
    var tooltipText = target.dataset.simpleTooltip;
    if(!tooltipText){
      return;
    }
    var html = buildSimpleTooltipHtml(tooltipText);
    if(!html){
      return;
    }
    simpleTooltip.innerHTML = html;
    simpleTooltip.style.display = 'block';
    simpleTooltipVisible = true;
    simpleTooltipTarget = target;
    positionTooltip(target, simpleTooltip);
  }

  function scheduleSimpleTooltip(target){
    if(!target){
      return;
    }
    cancelSimpleTooltipTimer();
    simpleTooltipTimer = setTimeout(function(){
      showSimpleTooltip(target);
    }, 500);
  }


  // Unified tooltip handler for better performance (event delegation)
  // WHY: Symbol tooltip is now only on .name-text elements, so it doesn't conflict with
  // buttons/indicators. Order doesn't matter much, but we check in a logical priority order.
  function handleUnifiedTooltipMouseOver(event){
    // Check for suppressed metric tooltip first (metric cells)
    var metric = event.target.closest('.metric[data-suppressed=""true""]');
    if(metric && metric.dataset.suppressionInfo){
      if(suppressionTooltipTarget && suppressionTooltipTarget !== metric){
        hideSuppressionTooltip();
      }
      if(tooltipVisible && tooltipTarget){ hideTooltip(); }
      if(symbolTooltipVisible && symbolTooltipTarget){ hideSymbolTooltip(); }
      if(simpleTooltipVisible && simpleTooltipTarget){ hideSimpleTooltip(); }
      scheduleSuppressionTooltip(metric);
      return;
    }
    // Check for symbol tooltip (name-text elements only)
    var nameElement = event.target.closest('.name-text[data-symbol-info], a.name-text[data-symbol-info]');
    if(nameElement && nameElement.dataset.symbolInfo){
      if(symbolTooltipTarget && symbolTooltipTarget !== nameElement){
        hideSymbolTooltip();
      }
      if(tooltipVisible && tooltipTarget){ hideTooltip(); }
      if(suppressionTooltipVisible && suppressionTooltipTarget){ hideSuppressionTooltip(); }
      if(simpleTooltipVisible && simpleTooltipTarget){ hideSimpleTooltip(); }
      scheduleSymbolTooltip(nameElement);
      return;
    }
    // Check for simple tooltip (buttons, indicators)
    var simpleTarget = event.target.closest('[data-simple-tooltip]');
    if(simpleTarget && simpleTarget.dataset.simpleTooltip){
      if(simpleTooltipTarget && simpleTooltipTarget !== simpleTarget){
        hideSimpleTooltip();
      }
      if(tooltipVisible && tooltipTarget){ hideTooltip(); }
      if(suppressionTooltipVisible && suppressionTooltipTarget){ hideSuppressionTooltip(); }
      if(symbolTooltipVisible && symbolTooltipTarget){ hideSymbolTooltip(); }
      scheduleSimpleTooltip(simpleTarget);
      return;
    }
  }

  function handleUnifiedTooltipMouseOut(event){
    var target = event.target;
    var related = event.relatedTarget;
    
    // WHY: Instant tooltip hiding - immediately cancel all pending timers and hide tooltips
    // when mouse leaves tooltip-triggering elements. We only keep tooltip visible if moving
    // to the tooltip element itself or staying within the triggering element.
    
    // Check if we're leaving a suppressed metric tooltip element
    if(suppressionTooltipVisible && suppressionTooltipTarget){
      if(related){
        // Check if moving to tooltip or still within the metric cell
        if(suppressionTooltip && suppressionTooltip.contains && suppressionTooltip.contains(related)){
          return; // Moving to tooltip - keep it visible
        }
        if(suppressionTooltipTarget.contains && suppressionTooltipTarget.contains(related)){
          return; // Still within metric cell - keep it visible
        }
      }
      // Moving away - hide immediately
      cancelSuppressionTooltipTimer();
      hideSuppressionTooltip();
      return;
    }
    
    // Check if we're leaving a symbol tooltip element (.name-text only)
    if(symbolTooltipVisible && symbolTooltipTarget){
      if(related){
        // Check if moving to tooltip or still within the name-text element
        if(symbolTooltip && symbolTooltip.contains && symbolTooltip.contains(related)){
          return; // Moving to tooltip - keep it visible
        }
        // WHY: Only check if still within the name-text element itself, not the parent cell
        // This ensures tooltip disappears when mouse leaves the text, not the entire cell
        if(symbolTooltipTarget.contains && symbolTooltipTarget.contains(related)){
          return; // Still within name-text element - keep it visible
        }
      }
      // Moving away from name-text - hide immediately
      cancelSymbolTooltipTimer();
      hideSymbolTooltip();
      return;
    }
    
    // Check if we're leaving a simple tooltip element (buttons, indicators)
    if(simpleTooltipVisible && simpleTooltipTarget){
      // WHY: For buttons/indicators, we want instant hiding when mouse leaves the element.
      // Hide immediately unless moving to the tooltip itself or staying within the button.
      if(!related){
        // No related target means mouse left the page - hide immediately
        cancelSimpleTooltipTimer();
        hideSimpleTooltip();
        return;
      }
      // Only keep visible if moving directly to the tooltip element
      if(simpleTooltip && simpleTooltip.contains && simpleTooltip.contains(related)){
        return; // Moving to tooltip - keep it visible
      }
      // Check if still within the button/indicator element itself
      // WHY: For buttons, this check ensures tooltip disappears immediately when leaving the button
      // even if moving to a sibling element in the same container
      if(simpleTooltipTarget.contains && simpleTooltipTarget.contains(related)){
        return; // Still within the element - keep it visible
      }
      // Moving away from button/indicator - hide immediately
      cancelSimpleTooltipTimer();
      hideSimpleTooltip();
      return;
    }
    
    // If no tooltip is visible but timers might be pending, cancel them all
    // This handles the case when mouse leaves before tooltip appears (after 500ms delay)
    cancelSuppressionTooltipTimer();
    cancelSymbolTooltipTimer();
    cancelSimpleTooltipTimer();
    
    // Also hide all visible tooltips as cleanup (shouldn't happen, but just in case)
    if(suppressionTooltipVisible){
      hideSuppressionTooltip();
    }
    if(symbolTooltipVisible){
      hideSymbolTooltip();
    }
    if(simpleTooltipVisible){
      hideSimpleTooltip();
    }
  }

  // Attach unified event listeners for better performance
  if(tbody){
    tbody.addEventListener(""mouseover"", handleUnifiedTooltipMouseOver, true);
    tbody.addEventListener(""mouseout"", handleUnifiedTooltipMouseOut, true);
  }

  var state = {
    rows: [],
    rowById: Object.create(null),
    childrenByParent: Object.create(null)
  };

  var ROOT_PARENT_KEY = '__root__';

  function normalizeParentKey(parentId){
    if(parentId === null || parentId === undefined || parentId === ''){
      return ROOT_PARENT_KEY;
    }
    return parentId;
  }

  function refreshState(){
    state.rows = Array.from(tbody.querySelectorAll('tr.node-row'));
    state.rowById = Object.create(null);
    state.childrenByParent = Object.create(null);

    state.rows.forEach(function(row){
      var id = row.getAttribute('data-id');
      if(id){
        state.rowById[id] = row;
      }
      var parentId = row.getAttribute('data-parent');
      var parentKey = normalizeParentKey(parentId);
      (state.childrenByParent[parentKey] || (state.childrenByParent[parentKey] = [])).push(row);
    });

    computeRowSeverity();
  }

  function computeRowSeverity(){
    state.rows.forEach(function(row){
      var metrics = row.querySelectorAll('.metric');
      var hasError = false;
      var hasWarning = false;
      var hasSuppressed = false;
      var hasDelta = false;
      for(var i = 0; i < metrics.length; i++){
        var metric = metrics[i];
        var isSuppressed = metric.dataset.suppressed === 'true';
        if(isSuppressed){
          hasSuppressed = true;
          // WHY: Suppressed metrics should not be counted as errors or warnings
          // because they are intentionally ignored via SuppressMessage attributes.
          continue;
        }
        var status = metric.dataset.status;
        if(status === 'error'){
          hasError = true;
        } else if(status === 'warning'){
          hasWarning = true;
        }
        if(!hasDelta){
          if(metric.dataset && metric.dataset.hasDelta === 'true'){
            hasDelta = true;
          } else {
            // WHY: Check for temporary classes (before JavaScript processing) and final classes (after processing)
            var deltaElement = metric.querySelector('.delta-positive, .delta-negative, .delta-improving, .delta-degrading');
            if(deltaElement){
              hasDelta = true;
            }
          }
        }
        if(hasError){
          break;
        }
      }
      row.dataset.hasError = hasError ? 'true' : 'false';
      row.dataset.hasWarning = hasWarning ? 'true' : 'false';
      row.dataset.hasSuppressed = hasSuppressed ? 'true' : 'false';
      row.dataset.hasDelta = hasDelta ? 'true' : 'false';
      var isNew = row.dataset.isNew === 'true' || !!row.querySelector('.badge-new');
      row.dataset.isNew = isNew ? 'true' : 'false';
      if(row.dataset.hiddenByAwareness === undefined){
        row.dataset.hiddenByAwareness = 'false';
      }
      if(row.dataset.hiddenByState === undefined){
        row.dataset.hiddenByState = 'false';
      }
    });
  }

  function directChildren(rowId){
    var key = normalizeParentKey(rowId);
    return state.childrenByParent[key] || [];
  }

  function getDescendantRows(rowId){
    var stack = directChildren(rowId).slice();
    var result = [];
    while(stack.length){
      var current = stack.pop();
      result.push(current);
      var currentId = current.getAttribute('data-id');
      var branch = directChildren(currentId);
      for(var i = 0; i < branch.length; i++){
        stack.push(branch[i]);
      }
    }
    return result;
  }

  var sortState = {
    column: null,
    direction: 'asc'
  };

  var numericValuePattern = /[^0-9.-]/g;

  function findMetricCell(row, col){
    var metrics = row.querySelectorAll('.metric');
    for(var i = 0; i < metrics.length; i++){
      var metric = metrics[i];
      if(metric.dataset && metric.dataset.col === col){
        return metric;
      }
    }
    return null;
  }

  function extractSortValue(row, col){
    if(col === 'symbol'){
      var nameCell = row.querySelector('.symbol .name-text');
      return nameCell ? nameCell.textContent.trim() : '';
    }
    var cell = findMetricCell(row, col);
    if(!cell){
      return '';
    }
    return cell.textContent.trim();
  }

  function getSortData(row, col){
    var text = extractSortValue(row, col);
    var numeric = parseFloat(text.replace(numericValuePattern, ''));
    return {
      text: text,
      numeric: numeric,
      hasNumeric: !isNaN(numeric)
    };
  }

  function compareRows(a, b, col, direction){
    var dataA = getSortData(a, col);
    var dataB = getSortData(b, col);

    if(dataA.hasNumeric && !dataB.hasNumeric){
      return -1;
    }
    if(!dataA.hasNumeric && dataB.hasNumeric){
      return 1;
    }

    var result;
    if(dataA.hasNumeric && dataB.hasNumeric){
      result = dataA.numeric - dataB.numeric;
    } else {
      result = dataA.text.localeCompare(dataB.text, undefined, { numeric: true, sensitivity: 'base' });
    }

    if(result === 0){
      var pathA = (a.getAttribute('data-fqn') || '').toLowerCase();
      var pathB = (b.getAttribute('data-fqn') || '').toLowerCase();
      result = pathA.localeCompare(pathB);
    }

    if(direction === 'desc'){
      result = -result;
    }

    return result;
  }

  function collectSubtreeRows(rootRow){
    var collected = [];
    (function visit(row){
      collected.push(row);
      var rowId = row.getAttribute('data-id');
      if(!rowId){
        return;
      }
      var children = directChildren(rowId);
      for(var i = 0; i < children.length; i++){
        visit(children[i]);
      }
    })(rootRow);
    return collected;
  }

  function clearSortIndicators(){
    var headers = table.querySelectorAll('thead th[data-col]');
    headers.forEach(function(header){
      header.classList.remove('sort-asc', 'sort-desc');
      header.removeAttribute('data-sort-direction');
    });
  }

  function applySortIndicator(th, direction){
    if(!th){
      return;
    }
    th.setAttribute('data-sort-direction', direction);
    th.classList.remove('sort-asc', 'sort-desc');
    th.classList.add(direction === 'asc' ? 'sort-asc' : 'sort-desc');
  }

  function sortGroup(parentId, children, col, direction){
    if(children.length <= 1){
      return;
    }

    var sortedChildren = children.slice().sort(function(a, b){
      return compareRows(a, b, col, direction);
    });

    var parentRow = parentId ? state.rowById[parentId] : null;
    if(!parentRow){
      sortedChildren.forEach(function(child){
        var subtreeRows = collectSubtreeRows(child);
        for(var i = 0; i < subtreeRows.length; i++){
          tbody.appendChild(subtreeRows[i]);
        }
      });
      return;
    }

    var anchor = parentRow;
    sortedChildren.forEach(function(child){
      var subtreeRows = collectSubtreeRows(child);
      for(var i = 0; i < subtreeRows.length; i++){
        var row = subtreeRows[i];
        tbody.insertBefore(row, anchor.nextSibling);
        anchor = row;
      }
    });
  }

  function sortHierarchy(col, direction){
    function sortLevel(parentId){
      var children = directChildren(parentId);
      if(children.length === 0){
        return;
      }

      sortGroup(parentId, children, col, direction);

      children.forEach(function(child){
        var childId = child.getAttribute('data-id');
        if(childId){
          sortLevel(childId);
        }
      });
    }

    sortLevel(null);
  }

  function setExpanderState(row, isExpanded){
    var hasChildren = row.dataset.hasChildren === 'true';
    var expander = row.querySelector('.expander');
    if(expander){
      expander.textContent = isExpanded ? '-' : '+';
    }
    row.dataset.expanded = hasChildren ? (isExpanded ? 'true' : 'false') : 'true';
  }

  function isAncestorExpanded(row){
    var parentId = row.getAttribute('data-parent');
    while(parentId){
      var parentRow = state.rowById[parentId];
      if(!parentRow){
        break;
      }
      if(isRowHidden(parentRow)){
        return false;
      }
      if(parentRow.dataset.expanded === 'false'){
        return false;
      }
      parentId = parentRow.getAttribute('data-parent');
    }
    return true;
  }

  var detailControl = document.getElementById('detail-level');
  var detailLabel = document.getElementById('detail-label');
  var detailLevels = {
    '1': { maxDepth: 1, label: 'Namespace' },
    '2': { maxDepth: 2, label: 'Type' },
    '3': { maxDepth: 3, label: 'Member' }
  };
  var currentDetail = detailLevels['2'];

  var awarenessControl = document.getElementById('awareness-level');
  var awarenessLabel = document.getElementById('awareness-label');
  var awarenessLevels = {
    '1': { label: 'All', predicate: function(row){ return true; } },
    '2': { label: 'Warning', predicate: function(row){ 
      // WHY: Suppressed metrics are intentionally ignored and should not appear in warning/error filters
      if(row.dataset.hasSuppressed === 'true'){ return false; }
      return row.dataset.hasError === 'true' || row.dataset.hasWarning === 'true'; 
    } },
    '3': { label: 'Error', predicate: function(row){ 
      // WHY: Suppressed metrics are intentionally ignored and should not appear in warning/error filters
      if(row.dataset.hasSuppressed === 'true'){ return false; }
      return row.dataset.hasError === 'true'; 
    } }
  };
  var currentAwarenessKey = '1';
  var currentAwareness = awarenessLevels[currentAwarenessKey];

  var newFilterControl = document.getElementById('filter-new');
  var changesFilterControl = document.getElementById('filter-changes');
  var suppressedFilterControl = document.getElementById('filter-suppressed');
  var stateFilter = {
    onlyNew: savedPreferences ? savedPreferences.filterNew === true : false,
    onlyChanges: savedPreferences ? savedPreferences.filterChanges === true : false,
    onlySuppressed: savedPreferences ? savedPreferences.filterSuppressed === true : false
  };

  function isRowHidden(row){
    return row.dataset.hiddenByDetail === 'true'
      || row.dataset.hiddenByFilter === 'true'
      || row.dataset.hiddenByAwareness === 'true'
      || row.dataset.hiddenByState === 'true';
  }

  function updateLeafClasses(){
    var maxDepth = currentDetail ? currentDetail.maxDepth : 2;
    state.rows.forEach(function(row){
      row.classList.remove('leaf-row');
      var expanderReset = row.querySelector('.expander');
      if(expanderReset){
        expanderReset.style.visibility = '';
        expanderReset.style.pointerEvents = '';
      }
    });

    state.rows.forEach(function(row){
      var role = row.dataset.role || 'member';
      var isStructural = role === 'assembly' || role === 'namespace' || role === 'type';
      var level = parseInt(row.getAttribute('data-level'), 10) || 0;
      var isDeepestLevel = level >= maxDepth;
      var hasChildren = row.dataset.hasChildren === 'true';
      var expander = row.querySelector('.expander');

      if(!hasChildren){
        if(isStructural && !isDeepestLevel){
          return;
        }
        row.classList.add('leaf-row');
        if(expander){
          expander.style.visibility = 'hidden';
          expander.style.pointerEvents = 'none';
        }
        return;
      }

      var hasEligibleChild = directChildren(row.getAttribute('data-id')).some(function(child){
        return !isRowHidden(child);
      });

      if(!hasEligibleChild || isDeepestLevel){
        row.classList.add('leaf-row');
        if(expander){
          expander.style.visibility = 'hidden';
          expander.style.pointerEvents = 'none';
        }
      }
    });
  }

  function updateStripedClasses(){
    state.rows.forEach(function(row){
      row.classList.remove('stripe-odd', 'stripe-even');
    });

    var visibleLeafRows = state.rows.filter(function(row){
      return row.classList.contains('leaf-row') && row.style.display !== 'none';
    });

    visibleLeafRows.forEach(function(row, index){
      row.classList.add(index % 2 === 0 ? 'stripe-odd' : 'stripe-even');
    });
  }

  function applyDetailLevel(maxDepth){
    state.rows.forEach(function(row){
      var level = parseInt(row.getAttribute('data-level'), 10) || 0;
      row.dataset.hiddenByDetail = level > maxDepth ? 'true' : 'false';
    });
    updateRowVisibility();
  }

  function updateRowVisibility(){
    state.rows.forEach(function(row){
      if(row.dataset.hiddenByAwareness === undefined){
        row.dataset.hiddenByAwareness = 'false';
      }
      var hidden = row.dataset.hiddenByDetail === 'true'
        || row.dataset.hiddenByFilter === 'true'
        || row.dataset.hiddenByAwareness === 'true'
        || row.dataset.hiddenByState === 'true';
      if(hidden){
        row.style.display = 'none';
      } else {
        row.style.display = isAncestorExpanded(row) ? '' : 'none';
      }
    });

    updateLeafClasses();
    updateStripedClasses();
  }

  function setDetailLevel(value, options){
    var level = detailLevels[value] || detailLevels['2'];
    currentDetail = level;
    if(detailLabel){
      detailLabel.textContent = level.label;
    }
    if(detailControl){
      detailControl.value = value;
      detailControl.setAttribute('aria-valuenow', value);
    }
    var expandAllRequested = options && (
      (options.expandAll === true)
      || (options.ctrlKey === true)
      || (options.shiftKey === true));

    if(expandAllRequested){
      expandAllNodes();
    }
    applyDetailLevel(level.maxDepth);
    applyStateFilters();
    persistPreferences();
  }

  function handleDetailChange(event){
    if(!detailControl){
      return;
    }
    var value = detailControl.value || '2';
    var expandWithModifier = event ? (event.ctrlKey || event.shiftKey) : false;
    setDetailLevel(value, { expandAll: expandWithModifier });
  }

  function applyAwarenessLevel(levelKey){
    var effectiveKey = awarenessLevels[levelKey] ? levelKey : '1';
    var level = awarenessLevels[effectiveKey];
    currentAwarenessKey = effectiveKey;
    currentAwareness = level;

    state.rows.forEach(function(row){
      row.dataset.hiddenByAwareness = 'true';
    });

    state.rows.forEach(function(row){
      if(level.predicate(row)){
        var currentRow = row;
        while(currentRow){
          currentRow.dataset.hiddenByAwareness = 'false';
          var parentId = currentRow.getAttribute('data-parent');
          currentRow = parentId ? state.rowById[parentId] : null;
        }
      }
    });

    updateRowVisibility();
  }

  function setAwarenessLevel(value, options){
    var key = awarenessLevels[value] ? value : '1';
    applyAwarenessLevel(key);
    if(awarenessControl){
      if(awarenessControl.value !== currentAwarenessKey){
        awarenessControl.value = currentAwarenessKey;
      }
      awarenessControl.setAttribute('aria-valuenow', currentAwarenessKey);
    }
    if(awarenessLabel && currentAwareness){
      awarenessLabel.textContent = currentAwareness.label;
    }
    persistPreferences();
  }

  function handleAwarenessChange(){
    if(!awarenessControl){
      return;
    }
    var value = awarenessControl.value || '1';
    setAwarenessLevel(value);
  }

  function snapSlider(control, event, setter){
    if(!control){
      return;
    }
    if(event.clientX === 0 && event.clientY === 0){
      return;
    }
    var rect = control.getBoundingClientRect();
    var width = rect.width;
    if(width <= 0){
      return;
    }
    var min = parseInt(control.min, 10);
    if(isNaN(min)){
      min = 1;
    }
    var max = parseInt(control.max, 10);
    if(isNaN(max)){
      max = 3;
    }
    if(max <= min){
      max = min;
    }
    var ratio = (event.clientX - rect.left) / width;
    ratio = Math.max(0, Math.min(1, ratio));
    var snapped = Math.round(ratio * (max - min)) + min;
    snapped = Math.max(min, Math.min(max, snapped));
    var snappedText = snapped.toString();
    if(control.value !== snappedText){
      control.value = snappedText;
    }
    setter(snappedText, event);
  }

  function collapseDescendants(parentId){
    getDescendantRows(parentId).forEach(function(descendant){
      setExpanderState(descendant, false);
    });
  }

  function expandAllNodes(){
    // WHY: Expanding the hierarchy is only done when explicitly requested (modifiers or initialization).
    state.rows.forEach(function(row){
      if(row.dataset.hasChildren === 'true'){
        setExpanderState(row, true);
      }
    });
  }

  function toggleRowExpansion(row, explicitState){
    if(!row){
      return;
    }
    if(row.dataset.hasChildren !== 'true'){
      return;
    }
    if(row.classList.contains('leaf-row')){
      return;
    }
    var rowId = row.getAttribute('data-id');
    if(!rowId){
      return;
    }
    var shouldExpand = typeof explicitState === 'boolean' ? explicitState : row.dataset.expanded === 'false';
    setExpanderState(row, shouldExpand);
    if(!shouldExpand){
      collapseDescendants(rowId);
    }
    applyDetailLevel(currentDetail.maxDepth);
  }

  refreshState();

  // WHY: Apply correct delta colors based on higherIsBetter flag from threshold data.
  // Delta colors should signal ""movement to better"", not just positive/negative.
  // Green = improving (moving to better), Red = degrading (moving to worse).
  function applyDeltaColors(){
    if(!thresholdData || !table){
      return;
    }
    var metricCells = table.querySelectorAll('.metric[data-metric-id]');
    metricCells.forEach(function(cell){
      var metricId = cell.dataset.metricId;
      if(!metricId){
        return;
      }
      var metricInfo = thresholdData[metricId];
      if(!metricInfo){
        return;
      }
      var higherIsBetter = metricInfo.higherIsBetter;
      if(typeof higherIsBetter !== 'boolean'){
        return;
      }
      var positiveDeltaNeutral = metricInfo.positiveDeltaNeutral === true;
      // WHY: Determine delta sign from CSS class (delta-positive/delta-negative) which is set
      // based on the actual numeric value in MetricValueRenderer. This is more reliable than
      // parsing text content and avoids fragility from formatting changes.
      var deltas = cell.querySelectorAll('.delta-positive, .delta-negative');
      deltas.forEach(function(delta){
        var isPositive = delta.classList.contains('delta-positive');
        var isImproving = higherIsBetter ? isPositive : !isPositive;
        delta.classList.remove('delta-positive', 'delta-negative');
        if (positiveDeltaNeutral && !higherIsBetter && isPositive){
          delta.classList.add('delta-neutral');
          return;
        }
        delta.classList.add(isImproving ? 'delta-improving' : 'delta-degrading');
      });
    });
  }

  // Initialize all rows: expand all nodes by default, set detail level to Type (2)
  // WHY: Users expect to see expanded tree structure and Type detail level by default
  // for better overview of the metrics hierarchy
  state.rows.forEach(function(row){
    if(row.dataset.hasChildren === 'true'){
      setExpanderState(row, true);
    } else {
      row.dataset.expanded = 'true';
    }
    row.dataset.hiddenByDetail = 'false';
    row.dataset.hiddenByFilter = 'false';
    row.dataset.hiddenByAwareness = row.dataset.hiddenByAwareness === undefined ? 'false' : row.dataset.hiddenByAwareness;
  });

  if(detailControl){
    if(savedPreferences && savedPreferences.detailLevel){
      detailControl.value = savedPreferences.detailLevel;
    }
    if(!detailControl.value){
      detailControl.value = '2';
    }
  }
  setDetailLevel(detailControl ? detailControl.value : '2', { expandAll: true });

  if(awarenessControl){
    if(savedPreferences && savedPreferences.awarenessLevel){
      awarenessControl.value = savedPreferences.awarenessLevel;
    }
    if(!awarenessControl.value){
      awarenessControl.value = '1';
    }
  }
  setAwarenessLevel(awarenessControl ? awarenessControl.value : '1');
  applyStateFilters();
  
  // Apply delta colors based on higherIsBetter flag
  applyDeltaColors();

  if(detailControl){
    detailControl.addEventListener('input', handleDetailChange);
    detailControl.addEventListener('change', handleDetailChange);
    detailControl.addEventListener('click', function(e){
      if(e.target !== detailControl){
        return;
      }
      snapSlider(detailControl, e, setDetailLevel);
    });
  }

  if(awarenessControl){
    awarenessControl.addEventListener('input', handleAwarenessChange);
    awarenessControl.addEventListener('change', handleAwarenessChange);
    awarenessControl.addEventListener('click', function(e){
      if(e.target !== awarenessControl){
        return;
      }
      snapSlider(awarenessControl, e, setAwarenessLevel);
    });
  }

  table.addEventListener('click', function(e){
    var actionButton = e.target.closest && e.target.closest('.row-action-icon');
    if(actionButton){
      e.stopPropagation();
      e.preventDefault();
      handleRowActionButton(actionButton);
      return;
    }
    var btn = e.target.closest('.expander');
    if(btn){
      e.stopPropagation();
      var parentId = btn.getAttribute('data-target');
      if(!parentId) return;
      var parentRow = state.rowById[parentId];
      if(!parentRow) return;
      var shouldExpand = parentRow.dataset.expanded === 'false';
      toggleRowExpansion(parentRow, shouldExpand);
      return;
    }

    var row = e.target.closest('tr.node-row');
    if(row && row.dataset.hasChildren === 'true' && !row.classList.contains('leaf-row')){
      if(!e.target.closest('button') && !e.target.closest('a') && !e.target.closest('input') && !e.target.closest('textarea') && !e.target.closest('select')){
        toggleRowExpansion(row);
        return;
      }
    }

    var th = e.target.closest('thead th');
    if(!th || !th.dataset.col){
      return;
    }

    var col = th.dataset.col;
    if(col !== 'symbol'){
      var hasMetricColumn = state.rows.some(function(r){
        return !!findMetricCell(r, col);
      });
      if(!hasMetricColumn){
        return;
      }
    }

    var direction;
    if(sortState.column === col){
      direction = sortState.direction === 'asc' ? 'desc' : 'asc';
    } else {
      direction = 'asc';
    }
    sortState.column = col;
    sortState.direction = direction;

    clearSortIndicators();
    var matchingHeaders = Array.from(table.querySelectorAll('thead th[data-col]')).filter(function(header){
      return header.dataset.col === col;
    });
    matchingHeaders.forEach(function(header){
      applySortIndicator(header, direction);
    });

    sortHierarchy(col, direction);
    refreshState();
    applyDetailLevel(currentDetail.maxDepth);
    applyStateFilters();
  });

  var expandBtn = document.getElementById('expand-all');
  if(expandBtn){
    expandBtn.addEventListener('click', function(){
      expandAllNodes();
      applyDetailLevel(currentDetail.maxDepth);
    });
  }

  var collapseBtn = document.getElementById('collapse-all');
  if(collapseBtn){
    collapseBtn.addEventListener('click', function(){
      state.rows.forEach(function(row){
        if(row.dataset.hasChildren === 'true'){
          setExpanderState(row, false);
        }
      });
      applyDetailLevel(currentDetail.maxDepth);
    });
  }

  var tableActions = document.querySelector('.table-actions');
  var theadRows = table ? Array.from(table.querySelectorAll('thead tr')) : [];
  function updateStickyHeaderPosition(){
    if(tableActions && theadRows.length > 0){
      var actionsHeight = tableActions.offsetHeight;
      var firstRow = theadRows[0];
      var firstRowThs = Array.from(firstRow.querySelectorAll('th'));
      var firstRowTop = actionsHeight - 1;
      firstRowThs.forEach(function(th){
        th.style.top = firstRowTop + 'px';
      });
      if(theadRows.length > 1){
        var secondRow = theadRows[1];
        var secondRowThs = Array.from(secondRow.querySelectorAll('th'));
        var firstRowHeight = firstRow.offsetHeight;
        var secondRowTop = firstRowTop + firstRowHeight - 1;
        secondRowThs.forEach(function(th){
          th.style.top = secondRowTop + 'px';
        });
      }
    }
  }

  updateStickyHeaderPosition();
  window.addEventListener('resize', updateStickyHeaderPosition);

  // Filter functionality
  var filterInput = document.getElementById('filter-input');
  var filterClear = document.getElementById('filter-clear');
  var currentFilter = '';

  function gatherPreferences(){
    var filterText = '';
    if(filterInput && typeof filterInput.value === 'string'){
      filterText = filterInput.value;
    }
    return {
      detailLevel: detailControl ? detailControl.value : '2',
      awarenessLevel: currentAwarenessKey,
      filterText: filterText,
      filterNew: stateFilter.onlyNew === true,
      filterChanges: stateFilter.onlyChanges === true,
      filterSuppressed: stateFilter.onlySuppressed === true
    };
  }

  function persistPreferences(){
    if(isRestoringPreferences){
      return;
    }
    writePreferences(gatherPreferences());
  }

  if(newFilterControl){
    newFilterControl.checked = stateFilter.onlyNew;
  }
  if(changesFilterControl){
    changesFilterControl.checked = stateFilter.onlyChanges;
  }
  if(suppressedFilterControl){
    suppressedFilterControl.checked = stateFilter.onlySuppressed;
  }
  if(filterInput && savedPreferences && typeof savedPreferences.filterText === 'string'){
    filterInput.value = savedPreferences.filterText;
  }

  if(filterInput){
    filterInput.addEventListener('keydown', function(event){
      if(event.key === 'Enter'){
        event.preventDefault();
        applyFilter(filterInput.value || '');
        filterInput.blur();
      }
    });
  }

  function updateFilterClearVisibility(){
    if(filterClear){
      filterClear.style.display = currentFilter.length > 0 ? '' : 'none';
    }
  }

  function applyFilter(filterText){
    currentFilter = (filterText || '').trim().toLowerCase();
    updateFilterClearVisibility();

    if(!currentFilter){
      // No filter - clear filter state and reapply detail level
      state.rows.forEach(function(row){
        row.dataset.hiddenByFilter = 'false';
      });
      applyStateFilters();
    persistPreferences();
      return;
    }

    // Build a map of matching rows and their ancestors
    var matchingRows = new Set();
    var matchingAncestors = new Set();

    // First pass: find all rows that match the filter
    state.rows.forEach(function(row){
      var fqn = row.getAttribute('data-fqn') || '';
      var matches = fqn.toLowerCase().includes(currentFilter);
      row.dataset.hiddenByFilter = matches ? 'false' : 'true';
      
      if(matches){
        matchingRows.add(row);
        // Add all ancestors to matching set
        var parentId = row.getAttribute('data-parent');
        while(parentId){
          var parentRow = state.rowById[parentId];
          if(parentRow){
            matchingAncestors.add(parentRow);
            parentId = parentRow.getAttribute('data-parent');
          } else {
            break;
          }
        }
      }
    });

    // Second pass: mark ancestors as matching
    matchingAncestors.forEach(function(row){
      row.dataset.hiddenByFilter = 'false';
    });

    // Reapply detail level which will now respect the filter
    applyStateFilters();
    persistPreferences();
  }

  function handleRowActionButton(button){
    if(!button){
      return;
    }
    var action = button.dataset.action;
    if(!action){
      return;
    }
    var row = button.closest('tr.node-row');
    if(!row){
      return;
    }
    if(action === 'open'){
      var sourcePath = (row.getAttribute('data-source-path') || '').trim();
      var sourceLine = (row.getAttribute('data-source-line') || '').trim();
      if(!sourcePath || !sourceLine){
        return;
      }
      var normalizedPath = sourcePath.replace(/\\/g, '/');
      var cursorUrl = 'cursor://file/' + normalizedPath + ':' + sourceLine;
      try{
        window.location.href = encodeURI(cursorUrl);
      }catch(error){
        console.warn('Failed to open file via Cursor protocol:', error);
      }
      return;
    }
    var fqn = (row.dataset.fqn || '').trim();
    if(!fqn){
      return;
    }
    if(action === 'copy'){
      copyTextToClipboard(fqn);
      return;
    }
    if(action === 'filter'){
      if(filterInput){
        filterInput.value = '';
        applyFilter('');
        filterInput.value = fqn;
        applyFilter(fqn);
        filterInput.focus();
      }
    }
  }

  if(filterInput){
    filterInput.addEventListener('input', function(e){
      applyFilter(e.target.value);
    });
    
    filterInput.addEventListener('keydown', function(e){
      // ESC key clears the filter
      if(e.key === 'Escape'){
        filterInput.value = '';
        applyFilter('');
      }
    });
  }

  if(filterClear){
    filterClear.addEventListener('click', function(){
      if(filterInput){
        filterInput.value = '';
        applyFilter('');
        filterInput.focus();
      }
    });
  }

  if(filterInput){
    applyFilter(filterInput.value || '');
  }
  isRestoringPreferences = false;

  function focusFilterInput(){
    if(!filterInput){
      return;
    }
    filterInput.focus();
    if(typeof filterInput.select === 'function'){
      filterInput.select();
    }
    updateFilterClearVisibility();
  }

  function clearFilterValue(){
    if(filterClear && filterClear.offsetParent !== null){
      filterClear.click();
      return;
    }
    if(filterInput){
      filterInput.value = '';
    }
    applyFilter('');
  }

  function changeDetailLevelBy(delta){
    var fallbackDetail = currentDetail && typeof currentDetail.maxDepth !== 'undefined'
      ? currentDetail.maxDepth
      : 2;
    var current = detailControl ? parseInt(detailControl.value, 10) : parseInt(fallbackDetail, 10);
    if(isNaN(current)){
      current = 2;
    }
    var next = Math.max(1, Math.min(3, current + delta));
    if(next === current){
      return;
    }
    setDetailLevel(next.toString(), {});
  }

  function changeAwarenessBy(delta){
    var current = parseInt(currentAwarenessKey, 10);
    if(isNaN(current)){
      current = 1;
    }
    var next = Math.max(1, Math.min(3, current + delta));
    if(next === current){
      return;
    }
    setAwarenessLevel(next.toString());
  }

  function toggleNewFilter(){
    if(!newFilterControl){
      return;
    }
    newFilterControl.checked = !newFilterControl.checked;
    handleNewFilterChange();
  }

  function toggleChangesFilter(){
    if(!changesFilterControl){
      return;
    }
    changesFilterControl.checked = !changesFilterControl.checked;
    handleChangesFilterChange();
  }

  function applyExpandAll(){
    if(expandBtn){
      expandBtn.click();
      return;
    }
    expandAllNodes();
  }

  function applyCollapseAll(){
    if(collapseBtn){
      collapseBtn.click();
      return;
    }
    state.rows.forEach(function(row){
      if(row.dataset.hasChildren === 'true'){
        setExpanderState(row, false);
      }
    });
    applyDetailLevel(currentDetail.maxDepth);
  }

  function isInputFocused(){
    var active = document.activeElement;
    if(!active || active === document.body){
      return false;
    }
    var tag = active.tagName;
    if(tag === 'INPUT' || tag === 'TEXTAREA' || active.isContentEditable){
      return true;
    }
    if(active.closest && active.closest('.row-action-icons')){
      return true;
    }
    return false;
  }

  function normalizeHotkeyKey(rawKey){
    if(!rawKey){
      return '';
    }
    var key = rawKey.toLowerCase();
    var map = {
      'т': 'n',
      'с': 'c',
      'а': 'f',
      'ч': 'x',
      'ф': 'a',
      'я': 'z',
      'у': 'e',
      'к': 'r',
      'в': 'd',
      'ы': 's'
    };
    return map[key] || key;
  }

  function handleHotkey(event){
    if(event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey){
      return;
    }
    if(isInputFocused()){
      return;
    }
    var key = normalizeHotkeyKey(event.key);
    switch(key){
      case 'n':
        toggleNewFilter();
        break;
      case 'c':
        toggleChangesFilter();
        break;
      case 'a':
        changeAwarenessBy(1);
        break;
      case 'z':
        changeAwarenessBy(-1);
        break;
      case 'd':
        changeDetailLevelBy(1);
        break;
      case 's':
        changeDetailLevelBy(-1);
        break;
      case 'f':
        focusFilterInput();
        break;
      case 'x':
        clearFilterValue();
        focusFilterInput();
        break;
      case 'e':
        applyExpandAll();
        break;
      case 'r':
        applyCollapseAll();
        break;
      default:
        return;
    }
    event.preventDefault();
    event.stopPropagation();
  }

  window.addEventListener('keydown', handleHotkey, true);

  function applyStateFilters(){
    var requireNew = stateFilter.onlyNew;
    var requireChanges = stateFilter.onlyChanges;
    var requireSuppressed = stateFilter.onlySuppressed;

    if(!requireNew && !requireChanges && !requireSuppressed){
      state.rows.forEach(function(row){
        row.dataset.hiddenByState = 'false';
      });
      updateRowVisibility();
      return;
    }

    state.rows.forEach(function(row){
      row.dataset.hiddenByState = 'true';
    });

    state.rows.forEach(function(row){
      var matchesNew = requireNew && row.dataset.isNew === 'true';
      var matchesChanges = requireChanges && row.dataset.hasDelta === 'true';
      var matchesSuppressed = requireSuppressed && row.dataset.hasSuppressed === 'true';
      var matches = matchesNew || matchesChanges || matchesSuppressed;
      if(matches){
        var current = row;
        while(current){
          current.dataset.hiddenByState = 'false';
          var parentId = current.getAttribute('data-parent');
          current = parentId ? state.rowById[parentId] : null;
        }
      }
    });

    updateRowVisibility();
  }

  function handleNewFilterChange(){
    if(!newFilterControl){
      return;
    }
    stateFilter.onlyNew = newFilterControl.checked;
    applyStateFilters();
    persistPreferences();
  }

  function handleChangesFilterChange(){
    if(!changesFilterControl){
      return;
    }
    stateFilter.onlyChanges = changesFilterControl.checked;
    applyStateFilters();
    persistPreferences();
  }

  function handleSuppressedFilterChange(){
    if(!suppressedFilterControl){
      return;
    }
    stateFilter.onlySuppressed = suppressedFilterControl.checked;
    applyStateFilters();
    persistPreferences();
  }

  if(newFilterControl){
    newFilterControl.addEventListener('change', handleNewFilterChange);
  }

  if(changesFilterControl){
    changesFilterControl.addEventListener('change', handleChangesFilterChange);
  }

  if(suppressedFilterControl){
    suppressedFilterControl.addEventListener('change', handleSuppressedFilterChange);
  }

  // Meta section spoiler toggle
  var metaSummary = document.querySelector('.meta-summary');
  var metaDetails = document.querySelector('.meta-details');
  if(metaSummary && metaDetails){
    var safeTransitionEnd = function(){
      if(!metaDetails.classList.contains('expanded')){
        metaDetails.style.display = 'none';
      }
    };
    metaSummary.addEventListener('click', function(){
      var isExpanded = metaSummary.classList.contains('expanded');
      var willExpand = metaDetails ? !isExpanded : false;
      metaSummary.classList.toggle('expanded', willExpand);
      if(willExpand){
        metaDetails.style.display = 'block';
        requestAnimationFrame(function(){
          metaDetails.classList.add('expanded');
        });
      } else {
        metaDetails.classList.remove('expanded');
        metaDetails.addEventListener('transitionend', function handler(event){
          if(event.propertyName === 'max-height' || event.propertyName === 'opacity'){
            safeTransitionEnd();
          }
        }, { once: true });
      }
    });
  }
})();";
}

