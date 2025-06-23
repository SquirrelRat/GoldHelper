using System.Collections.Generic;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace GoldHelper
{
    public class GoldHelperSettings : ISettings
    {
        [Menu("Enable Plugin")]
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        [Menu("Panel Layout (Vertical or Horizontal)")]
        public ListNode LayoutOrientation { get; set; } = new ListNode { Value = "Vertical", Values = new List<string> { "Vertical", "Horizontal" } };

        [Menu("Toggle Session Stats Panel")]
        public ToggleNode ShowSessionStats { get; set; } = new ToggleNode(true);
        
        [Menu("Toggle Graph in Session Panel")]
        public ToggleNode ShowGraphInSession { get; set; } = new ToggleNode(true);
        
        [Menu("Toggle Map Stats Panel")]
        public ToggleNode ShowMapStats { get; set; } = new ToggleNode(true);
        
        [Menu("Toggle Area Stats Panel")]
        public ToggleNode ShowAreaStats { get; set; } = new ToggleNode(true);
        
        [Menu("UI Horizontal Position (X)")]
        public RangeNode<int> PositionX { get; set; } = new RangeNode<int>(10, 0, 5000);

        [Menu("UI Vertical Position (Y)")]
        public RangeNode<int> PositionY { get; set; } = new RangeNode<int>(10, 0, 5000);

        [Menu("Main Content Text Color")]
        public ColorNode TextColor { get; set; } = new ColorNode(Color.White);

        [Menu("Panel Title Text Color")]
        public ColorNode TitleTextColor { get; set; } = new ColorNode(Color.White);

        [Menu("Panel Title Bar Color")]
        public ColorNode TitleBarColor { get; set; } = new ColorNode(new Color(0, 157, 255, 130));

        [Menu("Panel Background Color")]
        public ColorNode BackgroundColor { get; set; } = new ColorNode(new Color(0, 0, 0, 180));
        
        [Menu("Graph Bar #1 Color")]
        public ColorNode BarColor1 { get; set; } = new ColorNode(Color.Red);
        
        [Menu("Graph Bar #2 Color")]
        public ColorNode BarColor2 { get; set; } = new ColorNode(Color.DeepSkyBlue);
        
        [Menu("Graph Bar #3 Color")]
        public ColorNode BarColor3 { get; set; } = new ColorNode(Color.LimeGreen);
    }
}