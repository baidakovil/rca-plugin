namespace Rca.Tools.MetricsReporter.Rendering.Scripts;

/// <summary>
/// Defines keyboard shortcuts.
/// </summary>
internal static partial class JavascriptModules
{
  internal const string Hotkeys = @"
function initHotkeys(ctx){
  function isInputFocused(){
    const active = ctx.doc.activeElement;
    if(!active || active === ctx.doc.body){
      return false;
    }
    const tag = active.tagName;
    if(tag === 'INPUT' || tag === 'TEXTAREA' || active.isContentEditable){
      return true;
    }
    if(active.closest && active.closest('.row-action-icons')){
      return true;
    }
    return false;
  }

  window.addEventListener('keydown', function(event){
    if(event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey){
      return;
    }
    if(isInputFocused()){
      return;
    }
    const key = normalizeHotkeyKey(event.key);
    switch(key){
      case 'n':
        if(ctx.refs.newFilter){
          ctx.refs.newFilter.checked = !ctx.refs.newFilter.checked;
          ctx.stateFilter.onlyNew = ctx.refs.newFilter.checked;
          ctx.applyStateFilters();
          ctx.persistPreferences();
        }
        break;
      case 'c':
        if(ctx.refs.changesFilter){
          ctx.refs.changesFilter.checked = !ctx.refs.changesFilter.checked;
          ctx.stateFilter.onlyChanges = ctx.refs.changesFilter.checked;
          ctx.applyStateFilters();
          ctx.persistPreferences();
        }
        break;
      case 'f':
        if(ctx.refs.filterInput){
          ctx.refs.filterInput.focus();
          ctx.refs.filterInput.select();
        }
        break;
      case 'x':
        if(ctx.refs.filterInput){
          ctx.refs.filterInput.value = '';
          ctx.applyFilterText('');
          ctx.refs.filterInput.focus();
        }
        break;
      case 'd':
        ctx.setDetailLevel(String(Math.min(3, parseInt(ctx.currentDetailKey, 10) + 1)));
        break;
      case 's':
        ctx.setDetailLevel(String(Math.max(1, parseInt(ctx.currentDetailKey, 10) - 1)));
        break;
      case 'a':
        ctx.setAwarenessLevel(String(Math.min(3, parseInt(ctx.currentAwarenessKey, 10) + 1)));
        break;
      case 'z':
        ctx.setAwarenessLevel(String(Math.max(1, parseInt(ctx.currentAwarenessKey, 10) - 1)));
        break;
      case 'e':
        ctx.refs.expandButton && ctx.refs.expandButton.click();
        break;
      case 'r':
        ctx.refs.collapseButton && ctx.refs.collapseButton.click();
        break;
      default:
        return;
    }
    event.preventDefault();
    event.stopPropagation();
  }, true);
}
";
}

