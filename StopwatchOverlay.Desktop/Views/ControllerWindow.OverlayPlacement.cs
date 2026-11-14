using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using StopwatchOverlay.Desktop.Platform;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private sealed class DisplayOverlay(OverlayWindow window,Screen screen)
    {
        internal readonly OverlayWindow Window=window;
        internal readonly Screen Screen=screen;
        internal Size Size;
        internal Guid Session;
    }
    private readonly Dictionary<OverlayWindow,List<DisplayOverlay>> _displayOverlays=new();
    private IReadOnlyList<Screen>? _overlayScreens;

    private static string ScreenPositionKey(Screen screen)=>!string.IsNullOrEmpty(screen.DisplayName)?screen.DisplayName:$"{screen.Bounds.X},{screen.Bounds.Y}:{screen.Bounds.Width}x{screen.Bounds.Height}";

    private void WireOverlayCommands(OverlayWindow overlay,Func<TimerSession?> getSession)
    {
        overlay.CloseRequested+=()=>{if(getSession() is{} session)CloseSession(session);};
        overlay.PauseResumeRequested+=()=>{if(getSession() is{} session)ToggleSessionStartPause(session);};
        overlay.ResetRequested+=()=>{if(getSession() is{} session)ResetSession(session);};
        overlay.EditRequested+=()=>{if(getSession() is{} session)OpenTimerEditor(session);};
        overlay.ActivationRequested+=()=>{if(getSession() is{} session){_timerManager.Activate(session);RefreshTimerRail();UpdateActiveTimerDisplay();UpdateOverlayStates();}};
        overlay.SeparateMergeRequested+=()=>{if(getSession() is{} session){_timerManager.Activate(session);if(session.IsSeparated)MergeActiveSession();else SeparateActiveSession();}};
        overlay.PositionChangedByUser+=()=>
        {
            if(getSession() is not{} session)return;
            var screen=overlay.Screens.ScreenFromPoint(overlay.Position)??overlay.Screens.Primary;
            if(screen==null)return;
            session.HasCustomPosition=true;session.LastPresetPosition=_settings.Position;
            session.CustomLeft=overlay.Position.X;session.CustomTop=overlay.Position.Y;
            session.CustomPositionsByScreen[ScreenPositionKey(screen)]=(overlay.Position.X,overlay.Position.Y);
        };
    }

    private void ClearDisplayOverlays(OverlayWindow source)
    {
        if(!_displayOverlays.Remove(source,out var displays))return;
        foreach(var display in displays)if(display.Window!=source)display.Window.Close();
    }

    private void InvalidateOverlayPlacement(bool resetCustom=false)
    {
        if(resetCustom&&_settings.Position!="Custom")foreach(var session in _timerManager.Sessions)session.HasCustomPosition=false;
        foreach(var source in _displayOverlays.Keys.ToArray())ClearDisplayOverlays(source);
        _overlayScreens=null;
    }

    private void OverlayScreensChanged(object? sender,EventArgs e)
    {
        InvalidateOverlayPlacement();
        if(!_isExiting)UpdateOverlayStates();
    }

    private void SynchronizeOverlayDisplays(OverlayWindow source,TimerSession session)
    {
        if(!_displayOverlays.TryGetValue(source,out var displays))
        {
            _overlayScreens??=Screens.All;
            int index=SettingsChangePolicy.ResolveScreenComboIndex(_settings.ScreenIndex,_overlayScreens.Count);
            var targets=index==0?_overlayScreens.ToArray():new[]{_overlayScreens[index-1]};
            displays=new();_displayOverlays.Add(source,displays);
            for(int i=0;i<targets.Length;i++)
            {
                var window=i==0?source:new OverlayWindow();
                if(i>0)
                {
                    WireOverlayCommands(window,()=>source==_herdOverlay?GetActiveHerdSession():session);
                    ApplyOverlayStyle(window);window.SetClickThrough(_settings.ClickThrough);window.SetHideFromCapture(_settings.HideOverlayFromCapture);
                    window.Closed+=(_,_)=>
                    {
                        if(_isExiting||!_displayOverlays.ContainsKey(source))return;
                        var current=source==_herdOverlay?GetActiveHerdSession():session;
                        if(current!=null)_overlayVisibilityOverrides[current.Id]=false;
                        source.Hide();InvalidateOverlayPlacement();
                    };
                }
                displays.Add(new(window,targets[i]));
            }
        }
        foreach(var display in displays)
        {
            var window=display.Window;
            if(window!=source)
            {
                window.SetTimerName(session.Name);window.UpdateTime(FormatDisplayTime(session));window.SetRunning(session.IsRunning);window.SetPauseResumeEnabled(session.Mode!=1);
                window.SetRecIndicatorVisible(ShowTimerRec(session));
                bool active=session==_timerManager.Active;window.SetActive(active);window.SetInactiveSeparated(!active,StopwatchAppearancePolicy.ResolveInactiveOpacityPercent(_settings)/100.0);window.SetSeparateMergeState(session.IsSeparated);
                if(source.IsVisible&&!window.IsVisible)window.Show();else if(!source.IsVisible&&window.IsVisible)window.Hide();
            }
            if(display.Size==window.Bounds.Size&&display.Session==session.Id)continue;
            display.Size=window.Bounds.Size;display.Session=session.Id;
            bool custom=_settings.Position=="Custom"||(session.HasCustomPosition&&session.LastPresetPosition==_settings.Position);
            var point=session.CustomPositionsByScreen.TryGetValue(ScreenPositionKey(display.Screen),out var saved)?new PixelPoint((int)saved.Left,(int)saved.Top):session.HasCustomPosition?new((int)session.CustomLeft,(int)session.CustomTop):window.Position;
            var placed=OverlayScreenConstraint.PlaceOnScreen(custom?"Custom":_settings.Position,point,window.Bounds.Size,display.Screen);
            if(!custom&&session.IsSeparated)
            {
                int cascade=(int)Math.Round(24*((Math.Max(1,session.Number)-1)%8+1)*display.Screen.Scaling);
                placed=new(placed.X+(_settings.Position.EndsWith("Right")?-cascade:cascade),placed.Y+(_settings.Position.StartsWith("Bottom")?-cascade:cascade));
                placed=OverlayScreenConstraint.PlaceOnScreen("Custom",placed,window.Bounds.Size,display.Screen);
            }
            window.Position=placed;
        }
    }

    private void RefreshDisplayOverlayAppearance()
    {
        foreach(var (source,displays) in _displayOverlays)
            foreach(var display in displays)
                if(display.Window!=source){ApplyOverlayStyle(display.Window);display.Window.SetClickThrough(_settings.ClickThrough);display.Window.SetHideFromCapture(_settings.HideOverlayFromCapture);}
    }
}
