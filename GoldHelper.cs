using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Interfaces;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using System.IO;
using System.Text.Json;

namespace GoldHelper
{
    public class GoldHelper : BaseSettingsPlugin<GoldHelperSettings>
    {
        public class MapRunData
        {
            public string Name { get; set; }
            public long GoldGained { get; set; }
            public TimeSpan ElapsedTime { get; set; }
            public long GoldPerHour => ElapsedTime.TotalHours > 0 ? (long)(GoldGained / ElapsedTime.TotalHours) : 0;
            public DateTime CompletionTime { get; set; }
        }

        private TimeSpan _sessionElapsedTime;
        private long _sessionGoldGained;
        private int _completedMapCount;
        private long _totalMapGoldGained;

        private string _activeMapId;
        private string _activeMapName;
        private TimeSpan _activeMapElapsedTime;
        private long _activeMapGoldGained;
        private long _activeMapHash;

        private List<MapRunData> _recentMaps = new List<MapRunData>();
        private long _previousTotalGold;
        private DateTime _lastUpdateTime = DateTime.MinValue;

        private string _cachedSessionText = "";
        private string _cachedMapText = "";
        private string _cachedAreaText = "";
        private string _cachedMostProfitableMapText = "";

        private string _mapDataFilePath;

        public override bool Initialise()
        {
            Name = "Gold Helper";
            Settings.Layout.ResetButton.OnPressed = ResetAllStats;
            Settings.Data.ResetProfitabilityData.OnPressed = ResetProfitabilityData;

            if (string.IsNullOrEmpty(ConfigDirectory))
            {
                LogError("ConfigDirectory is not set. Data persistence will be disabled.", 5);
            }
            else
            {
                _mapDataFilePath = Path.Combine(ConfigDirectory, "map_runs_data.json");
                LoadMapRunsFromJson();
            }

            return true;
        }

        public override Job Tick()
        {
            var currentArea = GameController.Area.CurrentArea;
            if (GameController.InGame && !currentArea.IsPeaceful)
            {
                var deltaTime = TimeSpan.FromMilliseconds(GameController.DeltaTime);
                _sessionElapsedTime += deltaTime;

                if (currentArea.Hash == _activeMapHash)
                {
                    _activeMapElapsedTime += deltaTime;
                }
            }

            CheckAreaChange(currentArea);
            UpdateGoldTracking(currentArea);

            if (DateTime.Now - _lastUpdateTime >= TimeSpan.FromSeconds(1))
            {
                UpdateDisplayCache();
            }

            return null;
        }
        
        #region Render Methods
        public override void Render()
        {
            if (!Settings.Enable.Value || !GameController.InGame || IsAnyGameUIVisible())
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
            
            if (Settings.Data.ShowMostProfitableMap && Settings.Detached.ShowMostProfitablePanel.Value && !string.IsNullOrEmpty(_cachedMostProfitableMapText))
            {
                var drawPos = new Vector2(Settings.Detached.MostProfitablePanelX, Settings.Detached.MostProfitablePanelY);
                DrawSection(drawPos, "Most Profitable", _cachedMostProfitableMapText, false);
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
            
            var profitableHeader = "Most Profitable";
            var profitableHeaderSize = Graphics.MeasureText(profitableHeader, headerFontSize);
            var mostProfitableMapSize = Graphics.MeasureText(_cachedMostProfitableMapText, contentFontSize);

            const float minGraphWidth = 140f;
            float maxTextWidth = new[] { sessionSize.X, mapStatsSize.X, areaStatsSize.X, mainTitleSize.X, mostProfitableMapSize.X }.Max();
            float panelContentWidth = Math.Max(maxTextWidth, Settings.Graph.ShowGraph ? minGraphWidth : 0);
            float panelWidth = panelContentWidth + padding * 2;
            float graphHeight = Settings.Graph.ShowGraph ? 78f : 0;
            float calculatedContentHeight = (sessionHeaderSize.Y + 2) + sessionSize.Y + (Settings.Graph.ShowGraph ? (5 + graphHeight) : 0) +
                                            sectionSpacing + (mapStatsHeaderSize.Y + 2) + mapStatsSize.Y +
                                            sectionSpacing + (areaHeaderSize.Y + 2) + areaStatsSize.Y;

            if (Settings.Data.ShowMostProfitableMap && !string.IsNullOrEmpty(_cachedMostProfitableMapText))
            {
                calculatedContentHeight += sectionSpacing + (profitableHeaderSize.Y + 2) + mostProfitableMapSize.Y;
            }

            float totalContentHeight = padding + calculatedContentHeight + padding;
            var titleBarHeight = mainTitleSize.Y + padding;

            Graphics.DrawBox(new RectangleF(pos.X, pos.Y, panelWidth, titleBarHeight), Settings.Style.TitleBarColor);
            Graphics.DrawText("Gold Helper", new Vector2(pos.X + padding, pos.Y + titleBarHeight / 2 - mainTitleSize.Y / 2), Settings.Style.TitleTextColor, 16);
            Graphics.DrawBox(new RectangleF(pos.X, pos.Y + titleBarHeight, panelWidth, totalContentHeight), Settings.Style.BackgroundColor);

            var currentPos = new Vector2(pos.X + padding, pos.Y + titleBarHeight + padding);

            currentPos = DrawVerticalSessionSection(currentPos, sessionHeaderSize, sessionSize);
            if (Settings.Graph.ShowGraph)
            {
                currentPos.Y += 5;
                DrawGraph(currentPos, panelWidth - padding * 2);
                currentPos.Y += graphHeight;
            }
            
            currentPos.Y += sectionSpacing;
            currentPos = DrawVerticalMapStatsSection(currentPos, mapStatsHeaderSize, mapStatsSize);
            currentPos.Y += sectionSpacing;
            currentPos = DrawVerticalAreaSection(currentPos, areaHeaderSize, areaStatsSize);

            if (Settings.Data.ShowMostProfitableMap && !string.IsNullOrEmpty(_cachedMostProfitableMapText))
            {
                currentPos.Y += sectionSpacing;
                DrawVerticalMostProfitableSection(currentPos, profitableHeader, profitableHeaderSize);
            }
        }
        
        private Vector2 DrawVerticalSessionSection(Vector2 position, Vector2 headerSize, Vector2 contentSize)
        {
            Graphics.DrawText("Session", position, Settings.Style.AllInOneHeaderColor, 14);
            position.Y += headerSize.Y + 2;
            Graphics.DrawText(_cachedSessionText, position, Settings.Style.TextColor, 13);
            position.Y += contentSize.Y;
            return position;
        }

        private Vector2 DrawVerticalMapStatsSection(Vector2 position, Vector2 headerSize, Vector2 contentSize)
        {
            Graphics.DrawText("Map Stats", position, Settings.Style.AllInOneHeaderColor, 14);
            position.Y += headerSize.Y + 2;
            Graphics.DrawText(_cachedMapText, position, Settings.Style.TextColor, 13);
            position.Y += contentSize.Y;
            return position;
        }

        private Vector2 DrawVerticalAreaSection(Vector2 position, Vector2 headerSize, Vector2 contentSize)
        {
            Graphics.DrawText("Map", position, Settings.Style.AllInOneHeaderColor, 14);
            position.Y += headerSize.Y + 2;
            Graphics.DrawText(_cachedAreaText, position, Settings.Style.TextColor, 13);
            position.Y += contentSize.Y;
            return position;
        }

        private void DrawVerticalMostProfitableSection(Vector2 position, string header, Vector2 headerSize)
        {
            Graphics.DrawText(header, position, Settings.Style.AllInOneHeaderColor, 14);
            position.Y += headerSize.Y + 2;
            Graphics.DrawText(_cachedMostProfitableMapText, position, Settings.Style.TextColor, 13);
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
            var profitableMapHeader = "Most Profitable";

            var sessionTextSize = Graphics.MeasureText(_cachedSessionText, contentFontSize);
            var mapStatsTextSize = Graphics.MeasureText(_cachedMapText, contentFontSize);
            var areaStatsTextSize = Graphics.MeasureText(_cachedAreaText, contentFontSize);
            var profitableMapTextSize = Graphics.MeasureText(_cachedMostProfitableMapText, contentFontSize);

            var sessionHeaderSize = Graphics.MeasureText(sessionHeader, headerFontSize);
            var mapStatsHeaderSize = Graphics.MeasureText(mapStatsHeader, headerFontSize);
            var areaHeaderSize = Graphics.MeasureText(areaHeader, headerFontSize);
            var profitableMapHeaderSize = Graphics.MeasureText(profitableMapHeader, headerFontSize);

            float graphColWidth = Settings.Graph.ShowGraph ? 150f : 0;
            float graphHeight = Settings.Graph.ShowGraph ? 78f : 0;

            float sessionColWidth = Math.Max(sessionHeaderSize.X, sessionTextSize.X);
            float mapStatsColWidth = Math.Max(mapStatsHeaderSize.X, mapStatsTextSize.X);
            float areaColWidth = Math.Max(areaHeaderSize.X, areaStatsTextSize.X);
            float profitableMapColWidth = Math.Max(profitableMapHeaderSize.X, profitableMapTextSize.X);

            float totalWidth = padding + sessionColWidth + (Settings.Graph.ShowGraph ? (sectionSpacing + graphColWidth) : 0) +
                               sectionSpacing + mapStatsColWidth + sectionSpacing + areaColWidth;

            if (Settings.Data.ShowMostProfitableMap && !string.IsNullOrEmpty(_cachedMostProfitableMapText))
            {
                totalWidth += sectionSpacing + profitableMapColWidth;
            }

            float sessionColHeight = sessionHeaderSize.Y + 2 + sessionTextSize.Y;
            float mapStatsColHeight = mapStatsHeaderSize.Y + 2 + mapStatsTextSize.Y;
            float areaColHeight = areaHeaderSize.Y + 2 + areaStatsTextSize.Y;
            float profitableMapColHeight = profitableMapHeaderSize.Y + 2 + profitableMapTextSize.Y;

            float maxContentHeight = new[] { sessionColHeight, mapStatsColHeight, areaColHeight, graphHeight, profitableMapColHeight }.Max();

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

            if (Settings.Data.ShowMostProfitableMap && !string.IsNullOrEmpty(_cachedMostProfitableMapText))
            {
                currentPos.X += areaColWidth + sectionSpacing;
                Graphics.DrawText(profitableMapHeader, currentPos, Settings.Style.AllInOneHeaderColor, headerFontSize);
                Graphics.DrawText(_cachedMostProfitableMapText, new Vector2(currentPos.X, currentPos.Y + profitableMapHeaderSize.Y + 2), Settings.Style.TextColor, contentFontSize);
            }
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

            long maxVal = 1;
            if (Settings.Graph.GraphMode.Value == "Max Gold")
            {
                maxVal = _recentMaps.Any() ? _recentMaps.Max(m => m.GoldGained) : 1;
            }
            else
            {
                maxVal = _recentMaps.Any() ? _recentMaps.Max(m => m.GoldPerHour) : 1;
            }
            if (maxVal == 0) maxVal = 1;

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
                    long valueToDisplay = 0;

                    if (Settings.Graph.GraphMode.Value == "Max Gold")
                    {
                        valueToDisplay = barData.GoldGained;
                    }
                    else
                    {
                        valueToDisplay = barData.GoldPerHour;
                    }

                    var barHeight = (valueToDisplay / (float)maxVal) * graphArea.Height;
                    var barY = graphArea.Bottom - barHeight;
                    var barRect = new RectangleF(barX, barY, barWidth, barHeight);

                    Graphics.DrawBox(barRect, barColors[i]);

                    if (barRect.Contains(mousePosition))
                    {
                        var tooltipText = $"{barData.Name}\nGold Gained: {barData.GoldGained:N0}\nHourly Rate: {barData.GoldPerHour:N0}/hr";
                        var tooltipTextSize = Graphics.MeasureText(tooltipText);
                        var tooltipX = mousePosition.X + 15;
                        var tooltipY = mousePosition.Y - tooltipTextSize.Y - 5;
                        var tooltipPos = new Vector2(tooltipX, tooltipY);
                        var tooltipBgRect = new RectangleF(tooltipPos.X - 3, tooltipPos.Y - 3, tooltipTextSize.X + 6, tooltipTextSize.Y + 6);

                        Graphics.DrawBox(tooltipBgRect, Color.Black);
                        Graphics.DrawText(tooltipText, tooltipPos, Color.White);
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
        #endregion
        
        #region Logic Methods
        private void CheckAreaChange(AreaInstance currentArea)
        {
            bool isMap = !currentArea.IsPeaceful && !currentArea.Name.Contains("Hideout") && !currentArea.Name.Contains("Town");

            if (isMap)
            {
                if (_activeMapHash == 0)
                {
                    StartNewMap(currentArea);
                }
                else if (_activeMapHash != currentArea.Hash)
                {
                    FinalizeActiveMap();
                    StartNewMap(currentArea);
                }
            }
        }
        
        private void StartNewMap(AreaInstance area)
        {
            _activeMapId = area.Area.ToString();
            _activeMapName = area.Name;
            _activeMapElapsedTime = TimeSpan.Zero;
            _activeMapGoldGained = 0;
            _activeMapHash = area.Hash;
            LogMessage($"Started tracking new map: {_activeMapName}", 3);
        }

        private void FinalizeActiveMap()
        {
            if (_activeMapHash == 0 || _activeMapGoldGained <= 0)
            {
                _activeMapHash = 0;
                return;
            }

            LogMessage($"Map '{_activeMapName}' completed. Gained: {_activeMapGoldGained:N0} gold. Time: {_activeMapElapsedTime:hh\\:mm\\:ss}", 5);
            _completedMapCount++;
            _totalMapGoldGained += _activeMapGoldGained;
            var mapRun = new MapRunData
            {
                Name = _activeMapName,
                GoldGained = _activeMapGoldGained,
                ElapsedTime = _activeMapElapsedTime,
                CompletionTime = DateTime.Now
            };
            _recentMaps.Add(mapRun);

            while (_recentMaps.Count > Settings.Data.MaxMapsToKeep.Value)
            {
                _recentMaps.RemoveAt(0);
            }

            if (Settings.Data.EnablePersistence)
            {
                SaveMapRunsToJson();
            }
            
            _activeMapHash = 0;
            UpdateMostProfitableMaps();
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
                _sessionGoldGained += goldDifference;

                if (!currentArea.IsPeaceful && _activeMapHash != 0 && currentArea.Hash == _activeMapHash)
                {
                    _activeMapGoldGained += goldDifference;
                }
            }
            _previousTotalGold = currentTotalGold;
        }

        private void UpdateDisplayCache()
        {
            var sessionRate = _sessionElapsedTime.TotalHours > 0 ? _sessionGoldGained / _sessionElapsedTime.TotalHours : 0;
            _cachedSessionText = $"Time: {_sessionElapsedTime:hh\\:mm\\:ss}\n" + $"Gained: {_sessionGoldGained:N0}\n" + $"Rate: {FormatNumber(sessionRate)}/hr";

            var avgPerMap = _completedMapCount > 0 ? (double)_totalMapGoldGained / _completedMapCount : 0;
            _cachedMapText = $"Completed: {_completedMapCount}\n" + $"Avg. Gain: {avgPerMap:N0}";

            var areaRate = _activeMapElapsedTime.TotalHours > 0 ? _activeMapGoldGained / _activeMapElapsedTime.TotalHours : 0;
            _cachedAreaText = $"Time: {_activeMapElapsedTime:hh\\:mm\\:ss}\n" + $"Gained: {_activeMapGoldGained:N0}\n" + $"Rate: {FormatNumber(areaRate)}/hr";
        }

        private void UpdateMostProfitableMaps()
        {
            if (!_recentMaps.Any())
            {
                _cachedMostProfitableMapText = "No data yet.";
                return;
            }

            var profitableMaps = _recentMaps
                .GroupBy(run => run.Name)
                .Select(group => new
                {
                    Name = group.Key,
                    AverageHourly = group
                                  .OrderByDescending(run => run.CompletionTime)
                                  .Take(10)
                                  .Average(run => run.GoldPerHour)
                })
                .OrderByDescending(map => map.AverageHourly)
                .Take(3)
                .ToList();

            if (!profitableMaps.Any())
            {
                _cachedMostProfitableMapText = "No data yet.";
                return;
            }

            var sb = new StringBuilder();
            bool isFirstLine = true;
            foreach (var map in profitableMaps)
            {
                if (!isFirstLine) sb.AppendLine();
                sb.Append($"{map.Name} ({FormatNumber(map.AverageHourly)}/hr)");
                isFirstLine = false;
            }

            _cachedMostProfitableMapText = sb.ToString();
        }

        private string FormatNumber(double num)
        {
            if (num >= 1000000000)
                return (num / 1000000000D).ToString("0.#") + "b";
            if (num >= 1000000)
                return (num / 1000000D).ToString("0.#") + "m";
            if (num >= 1000)
                return (num / 1000D).ToString("0.#") + "k";

            return num.ToString("N0");
        }
        #endregion

        #region File IO Methods
        private void SaveMapRunsToJson()
        {
            if (string.IsNullOrEmpty(_mapDataFilePath))
            {
                return;
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                options.Converters.Add(new TimeSpanToStringConverter());
                var jsonString = JsonSerializer.Serialize(_recentMaps, options);
                File.WriteAllText(_mapDataFilePath, jsonString);
                LogMessage($"Map data saved to {_mapDataFilePath}", 0.1f);
            }
            catch (Exception ex)
            {
                LogError($"Failed to save map data: {ex.Message}", 5);
            }
        }

        private void LoadMapRunsFromJson()
        {
            if (string.IsNullOrEmpty(_mapDataFilePath) || !File.Exists(_mapDataFilePath))
            {
                _recentMaps = new List<MapRunData>();
                return;
            }

            try
            {
                var jsonString = File.ReadAllText(_mapDataFilePath);
                var options = new JsonSerializerOptions();
                options.Converters.Add(new TimeSpanToStringConverter());
                var loadedMaps = JsonSerializer.Deserialize<List<MapRunData>>(jsonString, options);
                if (loadedMaps != null)
                {
                    _recentMaps = loadedMaps;
                    while (_recentMaps.Count > Settings.Data.MaxMapsToKeep.Value)
                    {
                        _recentMaps.RemoveAt(0);
                    }
                    LogMessage($"Loaded {_recentMaps.Count} map runs from {_mapDataFilePath}", 0.1f);
                    UpdateMostProfitableMaps();
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to load map data: {ex.Message}", 5);
                _recentMaps = new List<MapRunData>();
            }
        }

        private class TimeSpanToStringConverter : System.Text.Json.Serialization.JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string value = reader.GetString();
                if (TimeSpan.TryParse(value, out TimeSpan result))
                {
                    return result;
                }
                return TimeSpan.Zero;
            }

            public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString("c"));
            }
        }

        private void ResetAllStats()
        {
            LogMessage("Resetting all stats.", 5);
            _sessionElapsedTime = TimeSpan.Zero;
            _sessionGoldGained = 0;
            _completedMapCount = 0;
            _totalMapGoldGained = 0;
            _previousTotalGold = 0;

            _activeMapId = null;
            _activeMapName = null;
            _activeMapElapsedTime = TimeSpan.Zero;
            _activeMapGoldGained = 0;
            _activeMapHash = 0;
            
            ResetProfitabilityData();
            
            UpdateDisplayCache();
        }

        private void ResetProfitabilityData()
        {
            LogMessage("Resetting map profitability data.", 5);
            _recentMaps.Clear();

            if (!string.IsNullOrEmpty(_mapDataFilePath) && File.Exists(_mapDataFilePath))
            {
                try
                {
                    File.Delete(_mapDataFilePath);
                    LogMessage("Map data file ('map_runs_data.json') has been deleted.", 5);
                }
                catch (Exception ex)
                {
                    LogError($"Failed to delete map data file: {ex.Message}", 5);
                }
            }
            UpdateMostProfitableMaps();
        }

        private long GetTotalGold()
        {
            var inventoryGold = GameController.IngameState.ServerData.Gold;
            var storageGold = GameController.IngameState.IngameUi.VillageScreen?.CurrentGold ?? 0;
            return inventoryGold + storageGold;
        }
        #endregion
    }
}