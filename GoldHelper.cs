using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Interfaces;
using SharpDX;
using Vector2 = System.Numerics.Vector2;

namespace GoldHelper
{
    public class GoldHelper : BaseSettingsPlugin<GoldHelperSettings>
    {
        private class MapRunData { public string Name { get; set; } public long GoldGained { get; set; } }
        
        private TimeSpan _sessionElapsedTime;
        private long _sessionGoldGained;
        private int _completedMapCount;
        private long _totalMapGoldGained;

        private string _activeMapId;
        private string _activeMapName;
        private TimeSpan _activeMapElapsedTime;
        private long _activeMapGoldGained;

        private readonly List<MapRunData> _recentMaps = new List<MapRunData>();
        private long _previousTotalGold;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private string _cachedSessionText = "";
        private string _cachedMapText = "";
        private string _cachedAreaText = "";

        public override bool Initialise()
        {
            Name = "Gold Helper";
            return true;
        }

        public override Job Tick()
        {
            var currentArea = GameController.Area.CurrentArea;
            if (GameController.InGame && !currentArea.IsPeaceful)
            {
                var deltaTime = TimeSpan.FromMilliseconds(GameController.DeltaTime);
                _sessionElapsedTime += deltaTime;

                if (!string.IsNullOrEmpty(_activeMapId) && currentArea.Area.ToString() == _activeMapId)
                {
                    _activeMapElapsedTime += deltaTime;
                }
            }
            
            CheckAreaChange(currentArea);
            UpdateGoldTracking(currentArea);
            
            if (DateTime.Now - _lastUpdateTime >= TimeSpan.FromSeconds(1))
            {
                UpdateDisplayCache();
                _lastUpdateTime = DateTime.Now;
            }
            
            return null;
        }
        
        public override void Render()
        {
            if (!Settings.Enable || !GameController.InGame || IsAnyGameUIVisible())
            {
                return;
            }
            
            if (Settings.Layout.Mode.Value == "All-in-one")
            {
                RenderAllInOneMode();
            }
            else
            {
                RenderDetachedMode();
            }
        }

        private void RenderDetachedMode()
        {
            if (Settings.Detached.ShowSessionStats)
            {
                var drawPos = new Vector2(Settings.Detached.SessionPanelX, Settings.Detached.SessionPanelY);
                DrawSection(drawPos, "Session Stats", _cachedSessionText, Settings.Graph.ShowGraph);
            }
            if (Settings.Detached.ShowMapStats)
            {
                var drawPos = new Vector2(Settings.Detached.MapStatsPanelX, Settings.Detached.MapStatsPanelY);
                DrawSection(drawPos, "Map Stats", _cachedMapText);
            }
            if (Settings.Detached.ShowAreaStats)
            {
                var drawPos = new Vector2(Settings.Detached.AreaPanelX, Settings.Detached.AreaPanelY);
                var areaTitle = string.IsNullOrEmpty(_activeMapName) ? "Active Map" : _activeMapName;
                DrawSection(drawPos, areaTitle, _cachedAreaText);
            }
        }
        
        private void RenderAllInOneMode()
        {
            if (Settings.AllInOne.Orientation.Value == "Vertical")
            {
                RenderAllInOneVertical();
            }
            else
            {
                RenderAllInOneHorizontal();
            }
        }

        private void RenderAllInOneVertical()
        {
            const int padding = 10;
            const int sectionSpacing = 15;
            const int headerFontSize = 14;
            const int contentFontSize = 13;
            
            var pos = new Vector2(Settings.AllInOne.PositionX, Settings.AllInOne.PositionY);

            var sessionSize = Graphics.MeasureText(_cachedSessionText, contentFontSize);
            var mapStatsSize = Graphics.MeasureText(_cachedMapText, contentFontSize);
            var areaStatsSize = Graphics.MeasureText(_cachedAreaText, contentFontSize);
            var mainTitleSize = Graphics.MeasureText("Gold Helper", 16);
            var sessionHeaderSize = Graphics.MeasureText("Session", headerFontSize);
            var mapStatsHeaderSize = Graphics.MeasureText("Map Stats", headerFontSize);
            var areaHeaderSize = Graphics.MeasureText("Map", headerFontSize);

            const float minGraphWidth = 140f;
            float maxTextWidth = new[] { sessionSize.X, mapStatsSize.X, areaStatsSize.X, mainTitleSize.X }.Max();
            float panelContentWidth = Math.Max(maxTextWidth, Settings.Graph.ShowGraph ? minGraphWidth : 0);
            float panelWidth = panelContentWidth + padding * 2;
            
            float graphHeight = Settings.Graph.ShowGraph ? 78f : 0;
            
            float calculatedContentHeight = (sessionHeaderSize.Y + 2) + sessionSize.Y + (Settings.Graph.ShowGraph ? (5 + graphHeight) : 0) +
                                            sectionSpacing + (mapStatsHeaderSize.Y + 2) + mapStatsSize.Y +
                                            sectionSpacing + (areaHeaderSize.Y + 2) + areaStatsSize.Y;
            float totalContentHeight = padding + calculatedContentHeight + padding;
            
            var titleBarHeight = mainTitleSize.Y + padding;
            
            Graphics.DrawBox(new RectangleF(pos.X, pos.Y, panelWidth, titleBarHeight), Settings.Style.TitleBarColor);
            Graphics.DrawText("Gold Helper", new Vector2(pos.X + padding, pos.Y + titleBarHeight / 2 - mainTitleSize.Y / 2), Settings.Style.TitleTextColor, 16);
            Graphics.DrawBox(new RectangleF(pos.X, pos.Y + titleBarHeight, panelWidth, totalContentHeight), Settings.Style.BackgroundColor);

            var currentPos = new Vector2(pos.X + padding, pos.Y + titleBarHeight + padding);

            Graphics.DrawText("Session", currentPos, Settings.Style.AllInOneHeaderColor, headerFontSize);
            currentPos.Y += sessionHeaderSize.Y + 2;
            Graphics.DrawText(_cachedSessionText, currentPos, Settings.Style.TextColor, contentFontSize);
            currentPos.Y += sessionSize.Y;

            if (Settings.Graph.ShowGraph)
            {
                currentPos.Y += 5;
                DrawGraph(currentPos, panelWidth - padding * 2);
                currentPos.Y += graphHeight;
            }
            
            currentPos.Y += sectionSpacing;
            
            Graphics.DrawText("Map Stats", currentPos, Settings.Style.AllInOneHeaderColor, headerFontSize);
            currentPos.Y += mapStatsHeaderSize.Y + 2;
            Graphics.DrawText(_cachedMapText, currentPos, Settings.Style.TextColor, contentFontSize);
            currentPos.Y += mapStatsSize.Y;
            
            currentPos.Y += sectionSpacing;

            Graphics.DrawText("Map", currentPos, Settings.Style.AllInOneHeaderColor, headerFontSize);
            currentPos.Y += areaHeaderSize.Y + 2;
            Graphics.DrawText(_cachedAreaText, currentPos, Settings.Style.TextColor, contentFontSize);
        }

        private void RenderAllInOneHorizontal()
        {
            const int padding = 10;
            const int sectionSpacing = 15;
            const int headerFontSize = 14;
            const int contentFontSize = 13;
            
            var pos = new Vector2(Settings.AllInOne.PositionX, Settings.AllInOne.PositionY);

            var sessionHeader = "Session";
            var mapStatsHeader = "Map Stats";
            var areaHeader = "Map";

            var sessionTextSize = Graphics.MeasureText(_cachedSessionText, contentFontSize);
            var mapStatsTextSize = Graphics.MeasureText(_cachedMapText, contentFontSize);
            var areaStatsTextSize = Graphics.MeasureText(_cachedAreaText, contentFontSize);
            
            var sessionHeaderSize = Graphics.MeasureText(sessionHeader, headerFontSize);
            var mapStatsHeaderSize = Graphics.MeasureText(mapStatsHeader, headerFontSize);
            var areaHeaderSize = Graphics.MeasureText(areaHeader, headerFontSize);
            
            float graphColWidth = Settings.Graph.ShowGraph ? 150f : 0;
            float graphHeight = Settings.Graph.ShowGraph ? 78f : 0;
            
            float sessionColWidth = Math.Max(sessionHeaderSize.X, sessionTextSize.X);
            float mapStatsColWidth = Math.Max(mapStatsHeaderSize.X, mapStatsTextSize.X);
            float areaColWidth = Math.Max(areaHeaderSize.X, areaStatsTextSize.X);
            
            float totalWidth = padding + sessionColWidth + (Settings.Graph.ShowGraph ? (sectionSpacing + graphColWidth) : 0) + 
                               sectionSpacing + mapStatsColWidth + sectionSpacing + areaColWidth + padding;

            float sessionColHeight = sessionHeaderSize.Y + 2 + sessionTextSize.Y;
            float mapStatsColHeight = mapStatsHeaderSize.Y + 2 + mapStatsTextSize.Y;
            float areaColHeight = areaHeaderSize.Y + 2 + areaStatsTextSize.Y;

            float maxContentHeight = new[] { sessionColHeight, mapStatsColHeight, areaColHeight, graphHeight }.Max();
            
            var mainTitleSize = Graphics.MeasureText("Gold Helper", 16);
            var titleBarHeight = mainTitleSize.Y + padding;
            
            Graphics.DrawBox(new RectangleF(pos.X, pos.Y, totalWidth, titleBarHeight), Settings.Style.TitleBarColor);
            Graphics.DrawText("Gold Helper", new Vector2(pos.X + padding, pos.Y + titleBarHeight / 2 - mainTitleSize.Y / 2), Settings.Style.TitleTextColor, 16);
            Graphics.DrawBox(new RectangleF(pos.X, pos.Y + titleBarHeight, totalWidth, maxContentHeight + padding * 2), Settings.Style.BackgroundColor);
            
            var currentPos = new Vector2(pos.X + padding, pos.Y + titleBarHeight + padding);
            
            Graphics.DrawText(sessionHeader, currentPos, Settings.Style.AllInOneHeaderColor, headerFontSize);
            Graphics.DrawText(_cachedSessionText, new Vector2(currentPos.X, currentPos.Y + sessionHeaderSize.Y + 2), Settings.Style.TextColor, contentFontSize);
            
            currentPos.X += sessionColWidth + sectionSpacing;
            
            if (Settings.Graph.ShowGraph)
            {
                DrawGraph(currentPos, graphColWidth);
                currentPos.X += graphColWidth + sectionSpacing;
            }
            
            Graphics.DrawText(mapStatsHeader, currentPos, Settings.Style.AllInOneHeaderColor, headerFontSize);
            Graphics.DrawText(_cachedMapText, new Vector2(currentPos.X, currentPos.Y + mapStatsHeaderSize.Y + 2), Settings.Style.TextColor, contentFontSize);

            currentPos.X += mapStatsColWidth + sectionSpacing;
            
            Graphics.DrawText(areaHeader, currentPos, Settings.Style.AllInOneHeaderColor, headerFontSize);
            Graphics.DrawText(_cachedAreaText, new Vector2(currentPos.X, currentPos.Y + areaHeaderSize.Y + 2), Settings.Style.TextColor, contentFontSize);
        }

        private void DrawGraph(Vector2 position, float width)
        {
            var mousePosition = Input.MousePosition;
            var barCount = Settings.Graph.GraphBarCount.Value;
            var graphArea = new RectangleF(position.X, position.Y, width, 50f);
            var axisColor = Settings.Style.TextColor.Value;
            axisColor.A = 150;
            Graphics.DrawBox(new RectangleF(graphArea.X, graphArea.Y, 2, graphArea.Height + 2), axisColor);
            Graphics.DrawBox(new RectangleF(graphArea.X, graphArea.Bottom, graphArea.Width, 2), axisColor);
            
            float totalBarAreaWidth = graphArea.Width * 0.7f;
            float totalSpacingWidth = graphArea.Width * 0.3f;
            float barWidth = totalBarAreaWidth / barCount;
            float spacing = totalSpacingWidth / (barCount + 1);

            var barColors = new[] { Settings.Graph.BarColor1.Value, Settings.Graph.BarColor2.Value, Settings.Graph.BarColor3.Value, Settings.Graph.BarColor4.Value, Settings.Graph.BarColor5.Value };
            long maxGold = _recentMaps.Any() ? _recentMaps.Max(m => m.GoldGained) : 1;
            if (maxGold == 0) maxGold = 1;

            for (int i = 0; i < barCount; i++)
            {
                var barX = graphArea.X + spacing + i * (barWidth + spacing);
                var labelText = (i + 1).ToString();
                var labelSize = Graphics.MeasureText(labelText, 12);
                var labelX = barX + barWidth / 2 - labelSize.X / 2;
                Graphics.DrawText(labelText, new Vector2(labelX, graphArea.Bottom + 5), Settings.Style.TextColor, 12);

                if (i < _recentMaps.Count)
                {
                    var barData = _recentMaps[i];
                    var barHeight = (barData.GoldGained / (float)maxGold) * graphArea.Height;
                    var barY = graphArea.Bottom - barHeight;
                    var barRect = new RectangleF(barX, barY, barWidth, barHeight);
                    
                    Graphics.DrawBox(barRect, barColors[i]);

                    if (barRect.Contains(mousePosition))
                    {
                        var mapName = barData.Name;
                        var tooltipTextSize = Graphics.MeasureText(mapName);
                        var tooltipX = mousePosition.X + 15;
                        var tooltipY = mousePosition.Y - 25;
                        var tooltipPos = new Vector2(tooltipX, tooltipY);
                        var tooltipBgRect = new RectangleF(tooltipPos.X - 3, tooltipPos.Y - 3, tooltipTextSize.X + 6, tooltipTextSize.Y + 6);
                        Graphics.DrawBox(tooltipBgRect, Color.Black);
                        Graphics.DrawText(mapName, tooltipPos, Color.White);
                    }
                }
            }
        }
        
        private Vector2 DrawSection(Vector2 position, string title, string content, bool drawGraph = false)
        {
            const int padding = 10;
            const int titleFontSize = 16;
            const int contentFontSize = 13;

            var titleSize = Graphics.MeasureText(title, titleFontSize);
            var contentSize = Graphics.MeasureText(content, contentFontSize);
            float contentMaxWidth = string.IsNullOrEmpty(content) ? 0 : content.Split('\n').Max(line => Graphics.MeasureText(line, contentFontSize).X);
            float width = Math.Max(titleSize.X, contentMaxWidth) + padding * 2;
            var titleBarHeight = titleSize.Y + padding;
            
            float contentTextHeight = contentSize.Y;
            float graphTotalHeight = 0;
            if (drawGraph)
            {
                const float graphHeight = 50f, labelHeight = 20f, graphTopPadding = 8f;
                graphTotalHeight = graphHeight + labelHeight + graphTopPadding;
            }
            var contentHeight = contentTextHeight + graphTotalHeight + padding;

            var titleBarRect = new RectangleF(position.X, position.Y, width, titleBarHeight);
            var contentBgRect = new RectangleF(position.X, position.Y + titleBarHeight, width, contentHeight);
            Graphics.DrawBox(titleBarRect, Settings.Style.TitleBarColor);
            Graphics.DrawBox(contentBgRect, Settings.Style.BackgroundColor);

            var titlePos = new Vector2(position.X + padding, position.Y + titleBarHeight / 2 - titleSize.Y / 2);
            Graphics.DrawText(title, titlePos, Settings.Style.TitleTextColor, titleFontSize);
            
            if (!string.IsNullOrEmpty(content))
            {
                var contentPos = new Vector2(position.X + padding, position.Y + titleBarHeight + (padding / 2f));
                Graphics.DrawText(content, contentPos, Settings.Style.TextColor, contentFontSize);
            }

            if (drawGraph)
            {
                DrawGraph(new Vector2(position.X + padding, position.Y + titleBarHeight + contentTextHeight + 15), width - padding * 2);
            }
            
            return new Vector2(width, titleBarHeight + contentHeight);
        }

        private bool IsAnyGameUIVisible()
        {
            var ui = GameController.IngameState.IngameUi;
            return ui.InventoryPanel.IsVisible ||
                   ui.OpenLeftPanel.IsVisible ||
                   ui.TreePanel.IsVisible ||
                   ui.AtlasPanel.IsVisible ||
                   ui.BetrayalWindow.IsVisible ||
                   ui.DelveWindow.IsVisible ||
                   ui.IncursionWindow.IsVisible ||
                   ui.HeistWindow.IsVisible ||
                   ui.ExpeditionWindow.IsVisible ||
                   ui.RitualWindow.IsVisible ||
                   ui.UltimatumPanel.IsVisible;
        }
        
        private void CheckAreaChange(AreaInstance currentArea)
        {
            var currentId = currentArea.Area.ToString();
            var isMap = !currentArea.IsPeaceful && !currentArea.Name.Contains("Hideout") && !currentArea.Name.Contains("Town");

            if (isMap)
            {
                if (currentId != _activeMapId)
                {
                    FinalizeActiveMap();
                    _activeMapId = currentId;
                    _activeMapName = currentArea.Name;
                    _activeMapElapsedTime = TimeSpan.Zero;
                    _activeMapGoldGained = 0;
                }
            }
        }

        private void FinalizeActiveMap()
        {
            if (string.IsNullOrEmpty(_activeMapId)) return;
            if (_activeMapGoldGained > 0)
            {
                LogMessage($"Map '{_activeMapName}' completed. Gained: {_activeMapGoldGained:N0} gold.", 5);
                _completedMapCount++;
                _totalMapGoldGained += _activeMapGoldGained;
                var mapRun = new MapRunData { Name = _activeMapName, GoldGained = _activeMapGoldGained };
                _recentMaps.Add(mapRun);
                
                while (_recentMaps.Count > Settings.Graph.GraphBarCount.Value)
                {
                    _recentMaps.RemoveAt(0);
                }
            }
            _activeMapId = null;
            _activeMapName = null;
        }
        
        private void UpdateGoldTracking(AreaInstance currentArea)
        {
            var currentTotalGold = GetTotalGold();
            if (_previousTotalGold == 0)
            {
                _previousTotalGold = currentTotalGold;
                return;
            }

            if (currentTotalGold > _previousTotalGold)
            {
                var goldDifference = currentTotalGold - _previousTotalGold;
                if (!currentArea.IsPeaceful)
                {
                    _sessionGoldGained += goldDifference;
                    if (!string.IsNullOrEmpty(_activeMapId) && currentArea.Area.ToString() == _activeMapId)
                    {
                        _activeMapGoldGained += goldDifference;
                    }
                }
            }
            _previousTotalGold = currentTotalGold;
        }

        private void UpdateDisplayCache()
        {
            var sessionRate = _sessionElapsedTime.TotalHours > 0 ? _sessionGoldGained / _sessionElapsedTime.TotalHours : 0;
            _cachedSessionText = $"Time: {_sessionElapsedTime:hh\\:mm\\:ss}\n" + $"Gained: {_sessionGoldGained:N0}\n" + $"Rate: {sessionRate:N0}/hr";
            
            var avgPerMap = _completedMapCount > 0 ? (double)_totalMapGoldGained / _completedMapCount : 0;
            _cachedMapText = $"Completed: {_completedMapCount}\n" + $"Avg. Gain: {avgPerMap:N0}";
            
            var areaRate = _activeMapElapsedTime.TotalHours > 0 ? _activeMapGoldGained / _activeMapElapsedTime.TotalHours : 0;
            _cachedAreaText = $"Time: {_activeMapElapsedTime:hh\\:mm\\:ss}\n" + $"Gained: {_activeMapGoldGained:N0}\n" + $"Rate: {areaRate:N0}/hr";
        }

        private long GetTotalGold()
        {
            var inventoryGold = GameController.IngameState.ServerData.Gold;
            var storageGold = GameController.IngameState.IngameUi.VillageScreen?.CurrentGold ?? 0;
            return inventoryGold + storageGold;
        }
    }
}