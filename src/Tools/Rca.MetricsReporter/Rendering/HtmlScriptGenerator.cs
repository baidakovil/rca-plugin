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

  var state = {
    rows: [],
    rowById: Object.create(null),
    childrenByParent: Object.create(null)
  };

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
      if(parentId){
        (state.childrenByParent[parentId] || (state.childrenByParent[parentId] = [])).push(row);
      }
    });

    computeRowSeverity();
  }

  function computeRowSeverity(){
    state.rows.forEach(function(row){
      var metrics = row.querySelectorAll('.metric');
      var hasError = false;
      var hasWarning = false;
      for(var i = 0; i < metrics.length; i++){
        var status = metrics[i].dataset.status;
        if(status === 'error'){
          hasError = true;
        } else if(status === 'warning'){
          hasWarning = true;
        }
        if(hasError){
          break;
        }
      }
      row.dataset.hasError = hasError ? 'true' : 'false';
      row.dataset.hasWarning = hasWarning ? 'true' : 'false';
      if(row.dataset.hiddenByAwareness === undefined){
        row.dataset.hiddenByAwareness = 'false';
      }
    });
  }

  function directChildren(rowId){
    return state.childrenByParent[rowId] || [];
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
    '2': { label: 'Warning', predicate: function(row){ return row.dataset.hasError === 'true' || row.dataset.hasWarning === 'true'; } },
    '3': { label: 'Error', predicate: function(row){ return row.dataset.hasError === 'true'; } }
  };
  var currentAwarenessKey = '1';
  var currentAwareness = awarenessLevels[currentAwarenessKey];

  function isRowHidden(row){
    return row.dataset.hiddenByDetail === 'true'
      || row.dataset.hiddenByFilter === 'true'
      || row.dataset.hiddenByAwareness === 'true';
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
        || row.dataset.hiddenByAwareness === 'true';
      if(hidden){
        row.style.display = 'none';
      } else {
        row.style.display = isAncestorExpanded(row) ? '' : 'none';
      }
    });

    updateLeafClasses();
    updateStripedClasses();
  }

  function setDetailLevel(value){
    var level = detailLevels[value] || detailLevels['2'];
    currentDetail = level;
    if(detailLabel){
      detailLabel.textContent = level.label;
    }
    if(detailControl){
      detailControl.setAttribute('aria-valuenow', value);
    }
    applyDetailLevel(level.maxDepth);
  }

  function handleDetailChange(){
    if(!detailControl){
      return;
    }
    var value = detailControl.value || '2';
    setDetailLevel(value);
  }

  function applyAwarenessLevel(levelKey){
    var effectiveKey = awarenessLevels[levelKey] ? levelKey : '1';
    var level = awarenessLevels[effectiveKey];
    currentAwarenessKey = effectiveKey;
    currentAwareness = level;

    state.rows.forEach(function(row){
      row.dataset.hiddenByAwareness = 'true';
      row.dataset.awarenessMatch = 'false';
    });

    state.rows.forEach(function(row){
      if(level.predicate(row)){
        row.dataset.awarenessMatch = 'true';
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

  function setAwarenessLevel(value){
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
    setter(snappedText);
  }

  function collapseDescendants(parentId){
    getDescendantRows(parentId).forEach(function(descendant){
      setExpanderState(descendant, false);
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
    row.dataset.awarenessMatch = row.dataset.awarenessMatch === undefined ? 'true' : row.dataset.awarenessMatch;
  });

  if(detailControl && !detailControl.value){
    detailControl.value = '2';
  }
  setDetailLevel(detailControl ? detailControl.value : '2');

  if(awarenessControl && !awarenessControl.value){
    awarenessControl.value = '1';
  }
  setAwarenessLevel(awarenessControl ? awarenessControl.value : '1');

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
    if(!th || !th.dataset.col) return;

    var col = th.dataset.col;
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

    var parents = Array.from(new Set(state.rows.map(function(r){
      return r.getAttribute('data-parent') || '';
    })));

    parents.forEach(function(parentId){
      var children = state.rows.filter(function(r){
        return (r.getAttribute('data-parent') || '') === parentId;
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

      var anchor = parentId ? state.rowById[parentId] : null;
      if(anchor){
        children.forEach(function(child){
          tbody.insertBefore(child, anchor.nextSibling);
          anchor = child;
        });
      } else {
        children.forEach(function(child){
          tbody.appendChild(child);
        });
      }
    });

    refreshState();
    applyDetailLevel(currentDetail.maxDepth);
  });

  var expandBtn = document.getElementById('expand-all');
  if(expandBtn){
    expandBtn.addEventListener('click', function(){
      state.rows.forEach(function(row){
        if(row.dataset.hasChildren === 'true'){
          setExpanderState(row, true);
        }
      });
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
      updateRowVisibility();
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
    updateRowVisibility();
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

  // Meta section spoiler toggle
  var metaSummary = document.querySelector('.meta-summary');
  var metaDetails = document.querySelector('.meta-details');
  if(metaSummary && metaDetails){
    metaSummary.addEventListener('click', function(){
      var isExpanded = metaSummary.classList.contains('expanded');
      if(isExpanded){
        metaSummary.classList.remove('expanded');
        metaDetails.style.display = 'none';
      } else {
        metaSummary.classList.add('expanded');
        metaDetails.style.display = '';
      }
    });
  }
})();";
}

