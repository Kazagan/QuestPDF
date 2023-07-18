using System.Collections.Generic;
using System.Linq;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace QuestPDF.Elements
{
    internal class MasonryElement : Container
    {
        
    }

    internal enum MasonryAlignment
    {
        Left,
        Center,
        Right,
        Justify,
        SpaceAround
    }

    internal struct MasonryMeasurement
    {
        public Element Element { get; set; }
        public SpacePlan Size { get; set; }
    }

    internal class Masonry : Element, IStateResettable, IContentDirectionAware
    {
        public ContentDirection ContentDirection { get; set; }
        public List<MasonryElement> Elements { get; set; } = new List<MasonryElement>();
        private Queue<MasonryElement> ChildrenQueue { get; set; }
        
        internal float VerticalSpacing { get; set; }
        internal float HorizontalSpacing { get; set; }
        
        internal MasonryAlignment? ElementsAlignment { get; set; }
        internal VerticalAlignment BaselineAlignment { get; set; }

        public void ResetState()
        {
            ChildrenQueue = new Queue<MasonryElement>(Elements);
        }

        internal override IEnumerable<Element?> GetChildren()
        {
            return Elements;
        }

        internal override SpacePlan Measure(Size availableSpace)
        {
            SetDefaultAlignment();

            if (!ChildrenQueue.Any())
                return SpacePlan.FullRender(Size.Zero);

            var lines = Compose(availableSpace);

            if (!lines.Any())
                return SpacePlan.Wrap();

            var lineSizes = lines
                .Select(line =>
                {
                    var size = GetLineSize(line);

                    var widthWithSpacing = size.Width + (line.Count - 1) * HorizontalSpacing;
                    return new Size(widthWithSpacing, size.Height);
                });
            
            var width = lineSizes.Max(x => x.Width);
            var height = lineSizes.Sum(x => x.Height) + (lines.Count - 1) * VerticalSpacing; // TODO
            var targetSize = new Size(width, height);

            var isPartiallyRendered = lines.Sum(x => x.Count) != ChildrenQueue.Count;
            
            if (isPartiallyRendered)
                return SpacePlan.PartialRender(targetSize);
            
            return SpacePlan.FullRender(targetSize);
        }

        internal override void Draw(Size availableSpace)
        {
            SetDefaultAlignment();

            var lines = Compose(availableSpace);
            var topOffset = 0f;

            foreach (var line in lines)
            {
                var height = line.Max(x => x.Size.Height); //TODO

                DrawLine(line);

                topOffset += height + VerticalSpacing;
                Canvas.Translate(new Position(0, -topOffset));
            }
            
            Canvas.Translate(new Position(0, -topOffset));
            lines.SelectMany(x => x).ToList().ForEach(x => ChildrenQueue.Dequeue());
            
            if(ChildrenQueue.Any())
                ResetState();

            void DrawLine(ICollection<MasonryMeasurement> lineMeasurements)
            {
                var lineSize = GetLineSize(lineMeasurements);

                var elementOffset = ElementOffset();
                var leftOffset = AlignOffset();

                foreach (var measurement in lineMeasurements)
                {
                    var size = (Size)measurement.Size;
                    var baselineOffset = BaselineOffset(size, lineSize.Height);

                    if (size.Height == 0)
                        size = new Size(size.Width, lineSize.Height);
                    
                    var offset = ContentDirection == ContentDirection.LeftToRight
                        ? new Position(leftOffset, baselineOffset)
                        : new Position(availableSpace.Width - size.Width - leftOffset, baselineOffset);
                    
                    Canvas.Translate(offset);
                    measurement.Element.Draw(size);
                    Canvas.Translate(offset.Reverse());

                    leftOffset += size.Width + elementOffset;
                }

                float ElementOffset()
                {
                    var difference = availableSpace.Width - lineSize.Width;

                    if (lineMeasurements.Count == 1)
                        return 0;

                    return ElementsAlignment switch
                    {
                        MasonryAlignment.Justify => difference / (lineMeasurements.Count - 1),
                        MasonryAlignment.SpaceAround => difference / (lineMeasurements.Count + 1),
                        _ => HorizontalSpacing
                    };
                }

                float AlignOffset()
                {
                    var emptySpace = availableSpace.Width - lineSize.Width - (lineMeasurements.Count - 1) * HorizontalSpacing;

                    return ElementsAlignment switch
                    {
                        MasonryAlignment.Left => ContentDirection == ContentDirection.LeftToRight ? 0 : emptySpace,
                        MasonryAlignment.Justify => 0,
                        MasonryAlignment.SpaceAround => elementOffset,
                        MasonryAlignment.Center => emptySpace / 2,
                        MasonryAlignment.Right => ContentDirection == ContentDirection.LeftToRight ? emptySpace : 0,
                        _ => 0
                    };
                }
                
                float BaselineOffset(Size elementSize, float lineHeight)
                {
                    var difference = lineHeight - elementSize.Height;

                    return BaselineAlignment switch
                    {
                        VerticalAlignment.Top => 0,
                        VerticalAlignment.Middle => difference / 2,
                        _ => difference
                    };
                }
            }
        }

        void SetDefaultAlignment()
        {
            if (ElementsAlignment.HasValue)
                return;

            ElementsAlignment = ContentDirection == ContentDirection.LeftToRight
                ? MasonryAlignment.Left
                : MasonryAlignment.Right;
        }

        Size GetLineSize(ICollection<MasonryMeasurement> measurements)
        {
            var width = measurements.Sum(x => x.Size.Width);
            var height = measurements.Max(x => x.Size.Height); // TODO

            return new Size(width, height);
        }

        private ICollection<ICollection<MasonryMeasurement>> Compose(Size availableSize)
        {
            var queue = new Queue<MasonryElement>(ChildrenQueue);
            var result = new List<ICollection<MasonryMeasurement>>();

            var topOffset = 0f;

            while (true)
            {
                var line = GetNextLine();
                if (!line.Any())
                    break;

                // So dealing with dynamic widths may be difficult here, due to the issue
                // of dynamic widths causing issues with gaps still appearing between elements.
                
                // looking at Pintrest as the golden goose of masonry, it appears to set the width on 
                // elements to have to be equivalent, Maybe start there for now. 
                var height = line.Max(x => x.Size.Height); 
                
                if(topOffset + height > availableSize.Height + Size.Epsilon)
                    break;

                topOffset += height + VerticalSpacing;
                result.Add(line);
            }

            return result;

            ICollection<MasonryMeasurement> GetNextLine()
            {
                var result = new List<MasonryMeasurement>();
                var leftOffset = GetInitialAlignmentOffset();

                while (true)
                {
                    if (!queue.Any())
                        break;

                    var nextElement = queue.Peek();
                    var elementSize = nextElement.Measure(new Size(availableSize.Width, Size.Max.Height));

                    if (elementSize.Type == SpacePlanType.Wrap)
                        break;
                    
                    if(leftOffset + elementSize.Width > availableSize.Width + Size.Epsilon)
                        break;

                    queue.Dequeue();
                    leftOffset += elementSize.Width + HorizontalSpacing;
                    
                    result.Add(new MasonryMeasurement()
                    {
                        Element = nextElement,
                        Size = elementSize
                    });
                }

                return result;
            }

            float GetInitialAlignmentOffset()
            {
                return ElementsAlignment switch
                {
                    MasonryAlignment.SpaceAround => HorizontalSpacing * 2,
                    _ => 0
                };
            }
        }
    }
}