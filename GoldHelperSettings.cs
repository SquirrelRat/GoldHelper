using System.Collections.Generic;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace GoldHelper
{
    public class GoldHelperSettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(true);
        public LayoutSettings Layout { get; set; } = new LayoutSettings();
        public AllInOneSettings AllInOne { get; set; } = new AllInOneSettings();
        public DetachedSettings Detached { get; set; } = new DetachedSettings();
        public StyleSettings Style { get; set; } = new StyleSettings();
        public GraphSettings Graph { get; set; } = new GraphSettings();
        public DataSettings Data { get; set; } = new DataSettings();
    }

    [Submenu(CollapsedByDefault = false)]
    public class LayoutSettings
    {
        [Menu("Layout Mode")]
        public ListNode Mode { get; set; } = new ListNode { Value = "All-in-one", Values = new List<string> { "All-in-one", "Detached" } };

        [Menu("Reset All Stats")]
        public ButtonNode ResetButton { get; set; } = new ButtonNode();
    }

    [Submenu(CollapsedByDefault = true)]
    public class AllInOneSettings
    {
        [Menu("Orientation")]
        public ListNode Orientation { get; set; } = new ListNode { Value = "Vertical", Values = new List<string> { "Vertical", "Horizontal" } };
        
        [Menu("Position X")]
        public RangeNode<int> PositionX { get; set; } = new RangeNode<int>(0, 0, 5000);

        [Menu("Position Y")]
        public RangeNode<int> PositionY { get; set; } = new RangeNode<int>(250, 0, 5000);
    }
    
    [Submenu(CollapsedByDefault = true)]
    public class DetachedSettings
    {
        [Menu("Show Session Stats Panel")]
        public ToggleNode ShowSessionStats { get; set; } = new ToggleNode(true);
        [Menu("Session Panel X")]
        public RangeNode<int> SessionPanelX { get; set; } = new RangeNode<int>(0, 0, 5000);
        [Menu("Session Panel Y")]
        public RangeNode<int> SessionPanelY { get; set; } = new RangeNode<int>(100, 0, 5000);

        [Menu("Show Map Stats Panel")]
        public ToggleNode ShowMapStats { get; set; } = new ToggleNode(true);
        [Menu("Map Stats Panel X")]
        public RangeNode<int> MapStatsPanelX { get; set; } = new RangeNode<int>(0, 0, 5000);
        [Menu("Map Stats Panel Y")]
        public RangeNode<int> MapStatsPanelY { get; set; } = new RangeNode<int>(270, 0, 5000);

        [Menu("Show Active Map Panel")]
        public ToggleNode ShowAreaStats { get; set; } = new ToggleNode(true);
        [Menu("Active Map Panel X")]
        public RangeNode<int> AreaPanelX { get; set; } = new RangeNode<int>(0, 0, 5000);
        [Menu("Active Map Panel Y")]
        public RangeNode<int> AreaPanelY { get; set; } = new RangeNode<int>(350, 0, 5000);

        [Menu("Show Most Profitable Panel")]
        public ToggleNode ShowMostProfitablePanel { get; set; } = new ToggleNode(true);
        [Menu("Most Profitable Panel X")]
        public RangeNode<int> MostProfitablePanelX { get; set; } = new RangeNode<int>(0, 0, 5000);
        [Menu("Most Profitable Panel Y")]
        public RangeNode<int> MostProfitablePanelY { get; set; } = new RangeNode<int>(450, 0, 5000);
    }
    
    [Submenu]
    public class StyleSettings
    {
        [Menu("Content Text")]
        public ColorNode TextColor { get; set; } = new ColorNode(Color.White);
        [Menu("Title Text")]
        public ColorNode TitleTextColor { get; set; } = new ColorNode(Color.White);
        [Menu("Title Bar")]
        public ColorNode TitleBarColor { get; set; } = new ColorNode(new Color(0, 157, 255, 80));
        [Menu("All-in-one Header Color")]
        public ColorNode AllInOneHeaderColor { get; set; } = new ColorNode(new Color(0, 157, 255, 255));
        [Menu("Background")]
        public ColorNode BackgroundColor { get; set; } = new ColorNode(new Color(0, 0, 0, 150));
    }
    
    [Submenu]
    public class GraphSettings
    {
        [Menu("Show Graph")]
        public ToggleNode ShowGraph { get; set; } = new ToggleNode(true);
        
        [Menu("Graph Mode")]
        public ListNode GraphMode { get; set; } = new ListNode { Value = "Max Gold", Values = new List<string> { "Max Gold", "Hourly Gold" } };

        [Menu("Bar Count")]
        public RangeNode<int> GraphBarCount { get; set; } = new RangeNode<int>(5, 3, 5);
        [Menu("Bar #1 Color")]
        public ColorNode BarColor1 { get; set; } = new ColorNode(Color.Red);
        [Menu("Bar #2 Color")]
        public ColorNode BarColor2 { get; set; } = new ColorNode(Color.DeepSkyBlue);
        [Menu("Bar #3 Color")]
        public ColorNode BarColor3 { get; set; } = new ColorNode(Color.LimeGreen);
        [Menu("Bar #4 Color")]
        public ColorNode BarColor4 { get; set; } = new ColorNode(Color.Orange);
        [Menu("Bar #5 Color")]
        public ColorNode BarColor5 { get; set; } = new ColorNode(Color.Violet);
    }

    [Submenu]
    public class DataSettings
    {
        [Menu("Enable Data Persistence")]
        public ToggleNode EnablePersistence { get; set; } = new ToggleNode(true);

        [Menu("Max Maps to Keep in History")]
        public RangeNode<int> MaxMapsToKeep { get; set; } = new RangeNode<int>(100, 25, 200);

        [Menu("Show Most Profitable Maps Panel")]
        public ToggleNode ShowMostProfitableMap { get; set; } = new ToggleNode(true);
        
        [Menu("Reset Profitability Data")]
        public ButtonNode ResetProfitabilityData { get; set; } = new ButtonNode();
    }
}