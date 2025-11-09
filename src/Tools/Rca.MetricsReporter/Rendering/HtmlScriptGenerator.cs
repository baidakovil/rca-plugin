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

  function getRows(){
    return Array.from(tbody.querySelectorAll('tr.node-row'));
  }

  var rowById = Object.create(null);
  var childrenByParent = Object.create(null);

  (function buildRowIndex(){
    var rows = getRows();
    rows.forEach(function(row){
      var id = row.getAttribute('data-id');
      if(id){
        rowById[id] = row;
      }
      var parentId = row.getAttribute('data-parent');
      if(parentId){
        if(!childrenByParent[parentId]){
          childrenByParent[parentId] = [];
        }
        childrenByParent[parentId].push(row);
      }
    });
  })();

  function getDescendantRows(parentId){
    var results = [];
    var stack = [];
    var directChildren = childrenByParent[parentId];
    if(directChildren){
      for(var i = 0; i < directChildren.length; i++){
        stack.push(directChildren[i]);
      }
    }
    while(stack.length > 0){
      var current = stack.pop();
      results.push(current);
      var currentId = current.getAttribute('data-id');
      var nested = childrenByParent[currentId];
      if(nested){
        for(var j = 0; j < nested.length; j++){
          stack.push(nested[j]);
        }
      }
    }
    return results;
  }

  function getDirectChildren(rowId){
    return childrenByParent[rowId] || [];
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
      var parentRow = rowById[parentId];
      if(!parentRow){
        break;
      }
      if(parentRow.dataset.hiddenByDetail === 'true'){
        return false;
      }
      if(parentRow.dataset.expanded === 'false'){
        return false;
      }
      parentId = parentRow.getAttribute('data-parent');
    }
    return true;
  }

  function updateLeafClasses(rows){
    var targetRows = rows || getRows();
    targetRows.forEach(function(row){
      row.classList.remove('leaf-row');
      var expanderReset = row.querySelector('.expander');
      if(expanderReset){
        expanderReset.style.visibility = '';
        expanderReset.style.pointerEvents = '';
      }
    });
    targetRows.forEach(function(row){
      var role = row.dataset.role || 'member';
      var isStructural = role === 'assembly' || role === 'namespace' || role === 'type';
      var level = parseInt(row.getAttribute('data-level'), 10) || 0;
      var isDeepestLevel = level >= currentDetail.maxDepth;
      var expander = row.querySelector('.expander');
      var hasChildren = row.dataset.hasChildren === 'true';
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
      var id = row.getAttribute('data-id');
      var children = getDirectChildren(id);
      var hasEligibleChild = children.some(function(child){
        return child.dataset.hiddenByDetail !== 'true';
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

  function updateStripedClasses(rows){
    var targetRows = rows || getRows();
    targetRows.forEach(function(row){
      row.classList.remove('stripe-odd', 'stripe-even');
    });
    var visibleLeafRows = targetRows.filter(function(row){
      return row.classList.contains('leaf-row') && row.style.display !== 'none';
    });
    visibleLeafRows.forEach(function(row, index){
      row.classList.add(index % 2 === 0 ? 'stripe-odd' : 'stripe-even');
    });
  }

  var detailControl = document.getElementById('detail-level');
  var detailLabel = document.getElementById('detail-label');
  var detailLevels = {
    '1': { maxDepth: 1, label: 'Namespace' },
    '2': { maxDepth: 2, label: 'Type' },
    '3': { maxDepth: 3, label: 'Member' }
  };
  var currentDetail = detailLevels['3'];

  function applyDetailLevel(maxDepth){
    var rows = getRows();
    rows.forEach(function(row){
      var level = parseInt(row.getAttribute('data-level'), 10) || 0;
      var hiddenByDetail = level > maxDepth ? 'true' : 'false';
      row.dataset.hiddenByDetail = hiddenByDetail;
      if(hiddenByDetail === 'true'){
        row.style.display = 'none';
        return;
      }
      row.style.display = isAncestorExpanded(row) ? '' : 'none';
    });
    updateLeafClasses(rows);
    updateStripedClasses(rows);
  }

  function handleDetailChange(){
    if(!detailControl){
      return;
    }
    var value = detailControl.value || '3';
    currentDetail = detailLevels[value] || detailLevels['3'];
    if(detailLabel){
      detailLabel.textContent = currentDetail.label;
    }
    detailControl.setAttribute('aria-valuenow', value);
    applyDetailLevel(currentDetail.maxDepth);
  }

  function snapDetailSlider(event){
    if(!detailControl){
      return;
    }
    if(event.clientX === 0 && event.clientY === 0){
      return;
    }
    var rect = detailControl.getBoundingClientRect();
    var width = rect.width;
    if(width <= 0){
      return;
    }
    var min = parseInt(detailControl.min, 10);
    if(isNaN(min)){
      min = 1;
    }
    var max = parseInt(detailControl.max, 10);
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
    if(detailControl.value !== snappedText){
      detailControl.value = snappedText;
    }
    handleDetailChange();
  }

  (function initVisibility(){
    var rows = getRows();
    rows.forEach(function(row){
      if(row.dataset.hasChildren === 'true'){
        setExpanderState(row, false);
      } else {
        row.dataset.expanded = 'true';
      }
      row.dataset.hiddenByDetail = 'false';
    });
    if(detailControl && !detailControl.value){
      detailControl.value = '3';
    }
    handleDetailChange();
  })();

  if(detailControl){
    detailControl.addEventListener('input', handleDetailChange);
    detailControl.addEventListener('change', handleDetailChange);
    detailControl.addEventListener('click', function(e){
      if(e.target !== detailControl){
        return;
      }
      snapDetailSlider(e);
    });
  }

  table.addEventListener('click', function(e){
    var btn = e.target.closest('.expander');
    if(btn){
      e.stopPropagation();
      var parentId = btn.getAttribute('data-target');
      if(!parentId) return;
      var parentRow = rowById[parentId];
      if(!parentRow) return;
      var shouldExpand = parentRow.dataset.expanded === 'false';
      setExpanderState(parentRow, shouldExpand);
      if(!shouldExpand){
        var descendants = getDescendantRows(parentId);
        descendants.forEach(function(descendant){
          setExpanderState(descendant, false);
        });
      }
      applyDetailLevel(currentDetail.maxDepth);
      return;
    }

    var th = e.target.closest('th');
    if(!th || !th.dataset.col) return;

    var col = th.dataset.col;
    var allRows = getRows();

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

    var parents = Array.from(new Set(allRows.map(function(r){
      return r.getAttribute('data-parent') || '';
    })));

    parents.forEach(function(parentId){
      var children = allRows.filter(function(r){
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

      if(parentId){
        childrenByParent[parentId] = children.slice();
      }

      var anchor = parentId ? rowById[parentId] : null;
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

    applyDetailLevel(currentDetail.maxDepth);
  });

  var expandBtn = document.getElementById('expand-all');
  if(expandBtn){
    expandBtn.addEventListener('click', function(){
      var rows = getRows();
      rows.forEach(function(row){
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
      var rows = getRows();
      rows.forEach(function(row){
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
})();";
}

