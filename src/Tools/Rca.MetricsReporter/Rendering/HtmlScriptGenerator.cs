namespace Rca.Tools.MetricsReporter.Rendering;

/// <summary>
/// Generates JavaScript code for the HTML metrics report.
/// Handles expand/collapse functionality, column sorting, and column width control.
/// </summary>
internal static class HtmlScriptGenerator
{
    /// <summary>
    /// Generates the complete JavaScript code for the metrics report.
    /// </summary>
    /// <returns>The JavaScript code as a string.</returns>
    public static string Generate()
        => @"(function(){
  var table = document.getElementById('metrics-table');
  if(!table) return;
  
  var tbody = table.tBodies[0];
  if(!tbody) return;
  
  var getRows = function(){ return Array.from(tbody.querySelectorAll('tr.node-row')); };
  
  // Initialize: hide all child rows (level > 0) on load, set all expanders to collapsed state
  (function initVisibility(){
    var rows = getRows();
    rows.forEach(function(r){
      var level = parseInt(r.getAttribute('data-level'), 10);
      if(level > 0){
        r.style.display = 'none';
      }
      // Set all expanders to collapsed state initially
      var expander = r.querySelector('.expander');
      if(expander){
        expander.textContent = '▸';
      }
    });
  })();
  
  // Helper: get all descendant rows of a parent
  function getDescendantRows(parentId, allRows){
    var descendants = [];
    var queue = [parentId];
    var visited = new Set();
    
    while(queue.length > 0){
      var currentId = queue.shift();
      if(visited.has(currentId)) continue;
      visited.add(currentId);
      
      allRows.forEach(function(r){
        if(r.getAttribute('data-parent') === currentId){
          descendants.push(r);
          queue.push(r.getAttribute('data-id'));
        }
      });
    }
    return descendants;
  }
  
  // Helper: toggle expander button state
  function setExpanderState(row, isExpanded){
    var expander = row.querySelector('.expander');
    if(expander){
      expander.textContent = isExpanded ? '▾' : '▸';
    }
  }
  
  // Expand/collapse by clicking expander
  table.addEventListener('click', function(e){
    var btn = e.target.closest('.expander');
    if(btn){
      e.stopPropagation();
      var parentId = btn.getAttribute('data-target');
      if(!parentId) return;
      
      var parentRow = tbody.querySelector('tr[data-id=\'' + parentId + '\']');
      if(!parentRow) return;
      
      var allRows = getRows();
      var descendants = getDescendantRows(parentId, allRows);
      
      // Determine current state: if any direct child is visible, consider expanded
      var parentLevel = parseInt(parentRow.getAttribute('data-level'), 10);
      var directChildren = descendants.filter(function(r){
        return parseInt(r.getAttribute('data-level'), 10) === parentLevel + 1;
      });
      var isCurrentlyExpanded = directChildren.some(function(r){
        return r.style.display !== 'none';
      });
      
      // Toggle: if expanded, collapse; if collapsed, expand
      var shouldExpand = !isCurrentlyExpanded;
      
      if(shouldExpand){
        // Expand: show direct children only
        directChildren.forEach(function(r){
          r.style.display = '';
        });
        setExpanderState(parentRow, true);
      } else {
        // Collapse: hide all descendants
        descendants.forEach(function(r){
          r.style.display = 'none';
          // Also collapse their expanders
          setExpanderState(r, false);
        });
        setExpanderState(parentRow, false);
      }
      
      return;
    }
    
    // Header sorting
    var th = e.target.closest('th');
    if(!th || !th.dataset.col) return;
    
    var col = th.dataset.col;
    var allRows = getRows();
    
    // Determine column index for metric data-col mapping
    var colIndex = -1;
    if(col === 'symbol'){
      colIndex = 0;
    } else {
      var headers = Array.from(table.querySelectorAll('thead th'));
      for(var i = 0; i < headers.length; i++){
        if(headers[i].dataset.col === col){
          colIndex = i;
          break;
        }
      }
    }
    
    if(colIndex < 0) return;
    
    // Group rows by parent and sort each group
    var parents = Array.from(new Set(allRows.map(function(r){
      return r.getAttribute('data-parent');
    })));
    
    parents.forEach(function(parentId){
      var children = allRows.filter(function(r){
        return r.getAttribute('data-parent') === parentId;
      });
      
      if(children.length <= 1) return;
      
      children.sort(function(a, b){
        var va = col === 'symbol' 
          ? (a.querySelector('.symbol .name-text') ? a.querySelector('.symbol .name-text').textContent.trim() : '')
          : (a.children[colIndex] ? a.children[colIndex].textContent.trim() : '');
        var vb = col === 'symbol'
          ? (b.querySelector('.symbol .name-text') ? b.querySelector('.symbol .name-text').textContent.trim() : '')
          : (b.children[colIndex] ? b.children[colIndex].textContent.trim() : '');
        
        var na = parseFloat(va.replace(/[^0-9.-]/g, ''));
        var nb = parseFloat(vb.replace(/[^0-9.-]/g, ''));
        
        if(!isNaN(na) && !isNaN(nb)){
          return na - nb;
        }
        return va.localeCompare(vb);
      });
      
      var parentRow = parentId ? tbody.querySelector('tr[data-id=\'' + parentId + '\']') : null;
      var anchor = parentRow || null;
      
      children.forEach(function(ch){
        tbody.insertBefore(ch, anchor ? anchor.nextSibling : tbody.firstChild);
      });
    });
  });
  
  // Expand all handler
  var expandBtn = document.getElementById('expand-all');
  if(expandBtn){
    expandBtn.addEventListener('click', function(){
      var rows = getRows();
      rows.forEach(function(r){
        r.style.display = '';
        setExpanderState(r, true);
      });
    });
  }
  
  // Collapse all handler
  var collapseBtn = document.getElementById('collapse-all');
  if(collapseBtn){
    collapseBtn.addEventListener('click', function(){
      var rows = getRows();
      rows.forEach(function(r){
        var level = parseInt(r.getAttribute('data-level'), 10);
        if(level > 0){
          r.style.display = 'none';
        }
        setExpanderState(r, false);
      });
    });
  }
  
  // Width control handlers
  var widthSlider = document.getElementById('symbol-width-slider');
  var widthDisplay = document.getElementById('symbol-width-display');
  var resetBtn = document.getElementById('reset-width');
  var defaultWidth = 420;
  var minWidth = 240;
  var maxWidth = 800;
  
  function applyWidth(width){
    if(isNaN(width) || width < minWidth || width > maxWidth){
      width = defaultWidth;
    }
    var elems = table.querySelectorAll('th[data-col=\'symbol\'], td.symbol');
    elems.forEach(function(e){
      e.style.width = width + 'px';
    });
    if(widthSlider){
      widthSlider.value = width;
    }
    if(widthDisplay){
      widthDisplay.textContent = width + 'px';
    }
  }
  
  function resetWidth(){
    applyWidth(defaultWidth);
  }
  
  if(widthSlider){
    widthSlider.min = minWidth;
    widthSlider.max = maxWidth;
    widthSlider.value = defaultWidth;
    widthSlider.addEventListener('input', function(){
      var w = parseInt(this.value, 10);
      applyWidth(w);
    });
  }
  
  if(resetBtn){
    resetBtn.addEventListener('click', resetWidth);
  }
  
  // Apply default width on load
  resetWidth();
})();";
}

