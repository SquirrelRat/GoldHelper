using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Interfaces;
using SharpDX;

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
            
            var drawPos = new System.Numerics.Vector2(Settings.PositionX, Settings.PositionY);
            const int margin = 5;

            if (Settings.ShowSessionStats)
            {
                var bounds = DrawSection(drawPos, "Session Stats", _cachedSessionText, Settings.ShowGraphInSession);
                if (Settings.LayoutOrientation.Value == "Vertical") drawPos.Y += bounds.Y + margin;
                else drawPos.X += bounds.X + margin;
            }
            if (Settings.ShowMapStats)
            {
                var bounds = DrawSection(drawPos, "Map Stats", _cachedMapText);
                if (Settings.LayoutOrientation.Value == "Vertical") drawPos.Y += bounds.Y + margin;
                else drawPos.X += bounds.X + margin;
            }
            if (Settings.ShowAreaStats)
            {
                var areaTitle = string.IsNullOrEmpty(_activeMapName) ? "Area: No active map" : $"Area: {_activeMapName}";
                DrawSection(drawPos, areaTitle, _cachedAreaText);
            }
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
                if (_recentMaps.Count > 3) _recentMaps.RemoveAt(0);
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

        private System.Numerics.Vector2 DrawSection(System.Numerics.Vector2 position, string title, string content, bool drawGraph = false)
        {
            var mousePosition = Input.MousePosition;
            const int padding = 10;
            const int titleFontSize = 16;
            const int contentFontSize = 13;

            var titleSize = Graphics.MeasureText(title, titleFontSize);
            var contentSize = Graphics.MeasureText(content, contentFontSize);
            float contentMaxWidth = content.Split('\n').Max(line => Graphics.MeasureText(line, contentFontSize).X);
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
            Graphics.DrawBox(titleBarRect, Settings.TitleBarColor);
            Graphics.DrawBox(contentBgRect, Settings.BackgroundColor);

            var titlePos = new System.Numerics.Vector2(position.X + padding, position.Y + titleBarHeight / 2 - titleSize.Y / 2);
            Graphics.DrawText(title, titlePos, Settings.TitleTextColor, titleFontSize);
            var contentPos = new System.Numerics.Vector2(position.X + padding, position.Y + titleBarHeight + (padding / 2f));
            Graphics.DrawText(content, contentPos, Settings.TextColor, contentFontSize);

            if (drawGraph)
            {
                const float graphTopPadding = 8f;
                var graphArea = new RectangleF(contentBgRect.X + padding, contentPos.Y + contentTextHeight + graphTopPadding, contentBgRect.Width - padding * 2, 50f);
                var axisColor = Settings.TextColor.Value;
                axisColor.A = 150;
                Graphics.DrawBox(new RectangleF(graphArea.X, graphArea.Y, 2, graphArea.Height + 2), axisColor);
                Graphics.DrawBox(new RectangleF(graphArea.X, graphArea.Bottom, graphArea.Width, 2), axisColor);
                float barWidth = graphArea.Width / 6f;
                var barColors = new[] { Settings.BarColor1.Value, Settings.BarColor2.Value, Settings.BarColor3.Value };
                long maxGold = _recentMaps.Any() ? _recentMaps.Max(m => m.GoldGained) : 1;
                if (maxGold == 0) maxGold = 1;

                for (int i = 0; i < 3; i++)
                {
                    float slotCenterX = graphArea.X + (graphArea.Width * (1 + 2 * i)) / 6f;
                    var labelText = (i + 1).ToString();
                    var labelSize = Graphics.MeasureText(labelText, 12);
                    var labelX = slotCenterX - labelSize.X / 2;
                    Graphics.DrawText(labelText, new System.Numerics.Vector2(labelX, graphArea.Bottom + 5), Settings.TextColor, 12);

                    if (i < _recentMaps.Count)
                    {
                        var barHeight = (_recentMaps[i].GoldGained / (float)maxGold) * graphArea.Height;
                        var barX = slotCenterX - barWidth / 2;
                        var barY = graphArea.Bottom - barHeight;
                        var barRect = new RectangleF(barX, barY, barWidth, barHeight);
                        
                        Graphics.DrawBox(barRect, barColors[i]);

                        if (barRect.Contains(mousePosition))
                        {
                            var mapName = _recentMaps[i].Name;
                            var tooltipTextSize = Graphics.MeasureText(mapName);
                            var tooltipX = mousePosition.X + 10;
                            var tooltipY = mousePosition.Y - 15;
                            var tooltipPos = new System.Numerics.Vector2(tooltipX, tooltipY);

                            var tooltipBgRect = new RectangleF(tooltipPos.X - 3, tooltipPos.Y - 3, tooltipTextSize.X + 6, tooltipTextSize.Y + 6);
                            Graphics.DrawBox(tooltipBgRect, Color.Black);
                            Graphics.DrawText(mapName, tooltipPos, Color.White);
                        }
                    }
                }
            }
            return new System.Numerics.Vector2(width, titleBarHeight + contentHeight);
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