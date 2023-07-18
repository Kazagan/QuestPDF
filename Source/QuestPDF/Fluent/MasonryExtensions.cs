using System;
using QuestPDF.Elements;
using QuestPDF.Infrastructure;

namespace QuestPDF.Fluent
{
    public class MasonryDescriptor
    {
        internal Masonry Masonry { get; } = new Masonry();

        public void Spacing(float value, Unit unit = Unit.Point)
        {
            VerticalSpacing(value, unit);
            HorizontalSpacing(value, unit);
        }

        public void VerticalSpacing(float value, Unit unit = Unit.Point)
        {
            Masonry.VerticalSpacing = value.ToPoints(unit);
        }
        
        public void HorizontalSpacing(float value, Unit unit = Unit.Point)
        {
            Masonry.HorizontalSpacing = value.ToPoints(unit);
        }
        
        public void BaselineTop() => Masonry.BaselineAlignment = VerticalAlignment.Top;
        public void BaselineMiddle() => Masonry.BaselineAlignment = VerticalAlignment.Middle;
        public void BaselineBottom() => Masonry.BaselineAlignment = VerticalAlignment.Bottom;

        internal void Alignment(MasonryAlignment? alignment) => Masonry.ElementsAlignment = alignment;
        public void AlignLeft() => Masonry.ElementsAlignment = MasonryAlignment.Left;
        public void AlignCenter() => Masonry.ElementsAlignment = MasonryAlignment.Center;
        public void AlignRight() => Masonry.ElementsAlignment = MasonryAlignment.Right;
        public void AlignJustify() => Masonry.ElementsAlignment = MasonryAlignment.Justify;
        public void AlignSpaceAround() => Masonry.ElementsAlignment = MasonryAlignment.SpaceAround;

        public IContainer Item()
        {
            var container = new MasonryElement();
            Masonry.Elements.Add(container);
            return container;
        }
    }

    public static class MasonryExtensions
    {
        public static void Masonry(this IContainer element, Action<MasonryDescriptor> handler)
        {
            var descriptor = new MasonryDescriptor();
            handler(descriptor);

            element.Element(descriptor.Masonry);
        }
    }
}