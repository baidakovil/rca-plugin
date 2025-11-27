namespace Rca.Tools.MetricsReporter.Rendering.Scripts;

/// <summary>
/// Defines tooltip functionality for the metrics report.
/// </summary>
internal static partial class JavascriptModules
{
  internal const string Tooltips = @"
function initTooltips(ctx){
  const host = createTooltipHost(ctx.doc);
  ctx.tooltipHost = host;

  const thresholdData = ctx.thresholdData;
  const ruleDescriptionsData = ctx.ruleDescriptionsData;

  function buildThresholdTooltip(metricId){
    if(!thresholdData || !thresholdData[metricId]){
      return null;
    }
    const info = thresholdData[metricId];
    const direction = info.higherIsBetter ? 'Higher values are better' : 'Lower values are better';
    const levels = info.levels || {};
    const levelOrder = ['Solution','Assembly','Namespace','Type','Member'];
    const levelLabels = {
      Solution: 'Solution',
      Assembly: 'Assembly',
      Namespace: 'Namespace',
      Type: 'Type',
      Member: 'Member'
    };
    const parts = [];
    if(info.description){
      parts.push('<p class=""metric-tooltip__desc""><em>' + escapeHtml(info.description) + '</em></p>');
    }
    parts.push('<p class=""metric-tooltip__direction""><em>' + escapeHtml(direction) + '</em></p>');
    parts.push('<p class=""metric-tooltip__heading""><strong>Warning / Error</strong></p>');
    parts.push('<ul class=""metric-tooltip__list"">');
    levelOrder.forEach(function(key){
      const entry = levels[key] || {};
      const warning = entry.warning !== undefined ? entry.warning : '—';
      const error = entry.error !== undefined ? entry.error : '—';
      parts.push('<li><strong>' + escapeHtml(levelLabels[key] || key) + ':</strong> <span>' + escapeHtml(String(warning)) + ' / ' + escapeHtml(String(error)) + '</span></li>');
    });
    parts.push('</ul>');
    return parts.join('');
  }

  function buildSuppressionTooltip(data){
    if(!data){
      return null;
    }
    const justification = data.justification
      ? data.justification
      : 'Suppressed via SuppressMessage.';
    return [
      '<p class=""metric-tooltip__heading""><strong>Suppressed ' + escapeHtml(data.ruleId || '') + '</strong></p>',
      '<p class=""metric-tooltip__desc"">' + justification + '</p>'
    ].join('');
  }

  function buildSymbolTooltip(symbolInfo){
    if(!symbolInfo){
      return null;
    }
    const parts = [];
    const role = symbolInfo.role || '';
    const fqn = symbolInfo.fullyQualifiedName || '';
    if(role){
      parts.push('<p class=""metric-tooltip__heading""><strong>' + escapeHtml(role) + '</strong></p>');
    }
    if(fqn){
      parts.push('<p class=""metric-tooltip__desc"">' + escapeHtml(fqn) + '</p>');
    }
    if(symbolInfo.sourcePath){
      let label = symbolInfo.sourcePath;
      if(typeof symbolInfo.sourceStartLine === 'number'){
        label += ':' + symbolInfo.sourceStartLine;
        if(typeof symbolInfo.sourceEndLine === 'number' && symbolInfo.sourceEndLine !== symbolInfo.sourceStartLine){
          label += '-' + symbolInfo.sourceEndLine;
        }
      }
      parts.push('<p class=""metric-tooltip__desc""><strong>Path:</strong> <code>' + escapeHtml(label) + '</code></p>');
    }
    return parts.join('');
  }

  function buildSimpleTooltip(text){
    if(!text){
      return null;
    }
    return '<p class=""metric-tooltip__desc"">' + escapeHtml(text) + '</p>';
  }

  function buildBreakdownTooltip(breakdown, metricId){
    if(!breakdown || typeof breakdown !== 'object' || Object.keys(breakdown).length === 0){
      return null;
    }
    const descriptions = ruleDescriptionsData || {};

    // Extract rule IDs and sort by number after CA/IDE prefix
    const ruleIds = Object.keys(breakdown).filter(function(ruleId){
      return breakdown[ruleId] > 0;
    });

    if(ruleIds.length === 0){
      return null;
    }

    // Sort by numeric part after CA/IDE prefix
    ruleIds.sort(function(a, b){
      const aMatch = a.match(/^(CA|IDE)(\d+)$/);
      const bMatch = b.match(/^(CA|IDE)(\d+)$/);
      if(!aMatch || !bMatch){
        return a.localeCompare(b);
      }
      const aNum = parseInt(aMatch[2], 10);
      const bNum = parseInt(bMatch[2], 10);
      if(aNum !== bNum){
        return aNum - bNum;
      }
      return aMatch[1].localeCompare(bMatch[1]);
    });

    const parts = [];
    ruleIds.forEach(function(ruleId){
      const count = breakdown[ruleId];
      const description = descriptions[ruleId];
      
      if(!description){
        // Fallback if description is missing
        parts.push('<p><strong>' + escapeHtml(ruleId) + ':</strong> <span style=""color: #0066cc;"">' + escapeHtml(String(count)) + '</span></p>');
        return;
      }

      const category = description.category || '';
      const shortDesc = description.shortDescription || '';
      const fullDesc = description.fullDescription || '';

      // Build description line: Category: Short description
      let descLine = '';
      if(category && shortDesc){
        descLine = escapeHtml(category) + ': ' + escapeHtml(shortDesc);
      } else if(shortDesc){
        descLine = escapeHtml(shortDesc);
      } else if(category){
        descLine = escapeHtml(category);
      }

      parts.push('<p><strong>' + escapeHtml(ruleId) + ':</strong> <span style=""color: #0066cc;"">' + escapeHtml(String(count)) + '</span></p>');
      
      if(descLine){
        parts.push('<p style=""margin-left: 8px; margin-top: 2px; margin-bottom: 4px;""><em>' + descLine + '</em></p>');
      }
      
      if(fullDesc){
        parts.push('<p style=""margin-left: 8px; margin-top: 2px; margin-bottom: 8px;""><em>' + escapeHtml(fullDesc) + '</em></p>');
      }
    });

    return parts.join('');
  }

  function attachTableHeaderTooltips(){
    const head = ctx.table.tHead;
    if(!head || !thresholdData){
      return;
    }
    head.addEventListener('mouseover', function(event){
      const th = event.target.closest('th[data-metric-id]');
      if(!th){
        return;
      }
      const metricId = th.dataset.metricId;
      if(!metricId || metricId === 'symbol'){
        return;
      }
      host.schedule(th, function(){
        return buildThresholdTooltip(metricId);
      });
    });
    head.addEventListener('mouseout', function(event){
      const th = event.target.closest('th[data-metric-id]');
      if(!th){
        return;
      }
      const related = event.relatedTarget;
      if(related && th.contains(related)){
        return;
      }
      host.hide();
    });
    head.addEventListener('focusin', function(event){
      const th = event.target.closest('th[data-metric-id]');
      if(!th){
        return;
      }
      const metricId = th.dataset.metricId;
      if(!metricId || metricId === 'symbol'){
        return;
      }
      host.show(th, function(){
        return buildThresholdTooltip(metricId);
      });
    }, true);
    head.addEventListener('focusout', function(event){
      const th = event.target.closest('th[data-metric-id]');
      if(!th){
        return;
      }
      host.hide();
    }, true);
  }

  function attachBodyTooltips(){
    const tbody = ctx.tbody;
    if(!tbody){
      return;
    }

    tbody.addEventListener('mouseover', function(event){
      const suppressedCell = event.target.closest('.metric[data-suppressed=""true""]');
      if(suppressedCell && suppressedCell.dataset.suppressionInfo){
        host.schedule(suppressedCell, function(){
          try{
            const data = JSON.parse(suppressedCell.dataset.suppressionInfo);
            return buildSuppressionTooltip(data);
          }catch(_){
            return null;
          }
        });
        return;
      }

      // Check for breakdown tooltip on SARIF metric cells
      const metricCell = event.target.closest('.metric[data-breakdown]');
      if(metricCell && metricCell.dataset.breakdown && metricCell.dataset.metricId){
        const metricId = metricCell.dataset.metricId;
        // Only show breakdown tooltip for SARIF metrics
        if(metricId === 'SarifCaRuleViolations' || metricId === 'SarifIdeRuleViolations'){
          host.schedule(metricCell, function(){
            try{
              const breakdown = JSON.parse(metricCell.dataset.breakdown);
              return buildBreakdownTooltip(breakdown, metricId);
            }catch(_){
              return null;
            }
          });
          return;
        }
      }

      const nameElement = event.target.closest('.name-text[data-symbol-info], a.name-text[data-symbol-info]');
      if(nameElement && nameElement.dataset.symbolInfo){
        host.schedule(nameElement, function(){
          try{
            return buildSymbolTooltip(JSON.parse(nameElement.dataset.symbolInfo));
          }catch(_){
            return null;
          }
        });
        return;
      }

      const simpleTarget = event.target.closest('[data-simple-tooltip]');
      if(simpleTarget && simpleTarget.dataset.simpleTooltip){
        host.schedule(simpleTarget, function(){
          return buildSimpleTooltip(simpleTarget.dataset.simpleTooltip);
        }, 260, true);
      }
    }, true);

    tbody.addEventListener('mouseout', function(event){
      const related = event.relatedTarget;
      if(related && host.anchor && (host.anchor.contains(related) || (host.element && host.element.contains && host.element.contains(related)))){
        return;
      }
      host.hide();
    }, true);
  }

  attachTableHeaderTooltips();
  attachBodyTooltips();
}
";
}

