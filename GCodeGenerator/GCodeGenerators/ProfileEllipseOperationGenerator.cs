using System;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class ProfileEllipseOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings)
        {
            if (!(operation is ProfileEllipseOperation op))
                return;

            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var toolRadius = op.ToolDiameter / 2.0;
            var offset = 0.0;
            if (op.ToolPathMode == ToolPathMode.Outside)
                offset = toolRadius;
            else if (op.ToolPathMode == ToolPathMode.Inside)
                offset = -toolRadius;

            var actualRadiusX = op.RadiusX + offset;
            var actualRadiusY = op.RadiusY + offset;

            var currentZ = op.ContourHeight;
            var finalZ = op.ContourHeight - op.TotalDepth;
            var passNumber = 0;

            var rotationRad = op.RotationAngle * Math.PI / 180.0;
            var startAngle = 0.0;
            var x_ellipse = actualRadiusX * Math.Cos(startAngle);
            var y_ellipse = actualRadiusY * Math.Sin(startAngle);
            var x_rotated = x_ellipse * Math.Cos(rotationRad) - y_ellipse * Math.Sin(rotationRad);
            var y_rotated = x_ellipse * Math.Sin(rotationRad) + y_ellipse * Math.Cos(rotationRad);
            var startX = op.CenterX + x_rotated;
            var startY = op.CenterY + y_rotated;

            while (currentZ > finalZ)
            {
                var nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ)
                    nextZ = finalZ;

                passNumber++;

                if (settings.UseComments)
                    addLine($"(Pass {passNumber}, depth {nextZ.ToString(fmt, culture)})");

                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                if (op.EntryMode == EntryMode.Vertical)
                {
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");
                }
                else
                {
                    var entryAngleRad = op.EntryAngle * Math.PI / 180.0;
                    var retractZ = currentZ + op.RetractHeight;
                    
                    addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    
                    var rampDepth = retractZ - nextZ;
                    var rampDistance = rampDepth / Math.Tan(entryAngleRad);
                    
                    var h = Math.Pow(actualRadiusX - actualRadiusY, 2) / Math.Pow(actualRadiusX + actualRadiusY, 2);
                    var perimeter = Math.PI * (actualRadiusX + actualRadiusY) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
                    
                    var angleForRamp = (rampDistance / perimeter) * 2 * Math.PI;
                    
                    var rampStartAngle = 0.0;
                    var rampEndAngle = angleForRamp;
                    if (op.Direction == MillingDirection.Clockwise)
                        rampEndAngle = -angleForRamp;
                    
                    var rampSegments = Math.Max(4, (int)(Math.Abs(angleForRamp) / (Math.PI / 16)));
                    for (int i = 1; i <= rampSegments; i++)
                    {
                        var t = (double)i / rampSegments;
                        var angle = rampStartAngle + t * rampEndAngle;
                        
                        x_ellipse = actualRadiusX * Math.Cos(angle);
                        y_ellipse = actualRadiusY * Math.Sin(angle);
                        
                        x_rotated = x_ellipse * Math.Cos(rotationRad) - y_ellipse * Math.Sin(rotationRad);
                        y_rotated = x_ellipse * Math.Sin(rotationRad) + y_ellipse * Math.Cos(rotationRad);
                        
                        var x = op.CenterX + x_rotated;
                        var y = op.CenterY + y_rotated;
                        var z = retractZ - t * rampDepth;
                        
                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} Z{z.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                    
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{nextZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                var h_perimeter = Math.Pow(actualRadiusX - actualRadiusY, 2) / Math.Pow(actualRadiusX + actualRadiusY, 2);
                var ellipsePerimeter = Math.PI * (actualRadiusX + actualRadiusY) * (1 + 3 * h_perimeter / (10 + Math.Sqrt(4 - 3 * h_perimeter)));
                var numSegments = Math.Max(8, (int)Math.Ceiling(ellipsePerimeter / op.MaxSegmentLength));
                var angleStep = 2 * Math.PI / numSegments;
                
                if (op.Direction == MillingDirection.Clockwise)
                    angleStep = -angleStep;
                
                for (int i = 1; i <= numSegments; i++)
                {
                    var angle = i * angleStep;
                    
                    x_ellipse = actualRadiusX * Math.Cos(angle);
                    y_ellipse = actualRadiusY * Math.Sin(angle);
                    
                    x_rotated = x_ellipse * Math.Cos(rotationRad) - y_ellipse * Math.Sin(rotationRad);
                    y_rotated = x_ellipse * Math.Sin(rotationRad) + y_ellipse * Math.Cos(rotationRad);
                    
                    var x = op.CenterX + x_rotated;
                    var y = op.CenterY + y_rotated;
                    
                    addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }

                if (nextZ > finalZ)
                {
                    var retractZAfterPass = nextZ + op.RetractHeight;
                    addLine($"{g0} Z{retractZAfterPass.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                currentZ = nextZ;
            }

            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
        }
    }
}

