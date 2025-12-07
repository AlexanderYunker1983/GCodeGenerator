using System;
using System.Collections.Generic;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class ProfilePolygonOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings)
        {
            if (!(operation is ProfilePolygonOperation op))
                return;

            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var toolRadius = op.ToolDiameter / 2.0;
            var offset = 0.0;
            if (op.ToolPathMode == ToolPathMode.Outside)
                offset = toolRadius;
            else if (op.ToolPathMode == ToolPathMode.Inside)
                offset = -toolRadius;

            var actualRadius = op.Radius + offset;

            var currentZ = op.ContourHeight;
            var finalZ = op.ContourHeight - op.TotalDepth;
            var passNumber = 0;

            var rotationRad = op.RotationAngle * Math.PI / 180.0;
            var angleStep = 2 * Math.PI / op.NumberOfSides;
            var vertices = new List<(double x, double y)>();
            
            for (int i = 0; i < op.NumberOfSides; i++)
            {
                var angle = i * angleStep + rotationRad;
                var x = op.CenterX + actualRadius * Math.Cos(angle);
                var y = op.CenterY + actualRadius * Math.Sin(angle);
                vertices.Add((x, y));
            }

            var startX = vertices[0].x;
            var startY = vertices[0].y;

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
                    
                    var edgeLength = Math.Sqrt(Math.Pow(vertices[1].x - vertices[0].x, 2) + Math.Pow(vertices[1].y - vertices[0].y, 2));
                    var rampRatio = Math.Min(1.0, rampDistance / edgeLength);
                    
                    var rampSegments = Math.Max(4, (int)(rampRatio * 20));
                    for (int i = 1; i <= rampSegments; i++)
                    {
                        var t = (double)i / rampSegments * rampRatio;
                        var x = vertices[0].x + t * (vertices[1].x - vertices[0].x);
                        var y = vertices[0].y + t * (vertices[1].y - vertices[0].y);
                        var z = retractZ - t * rampDepth / rampRatio;
                        
                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} Z{z.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                    
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{nextZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                if (op.Direction == MillingDirection.Clockwise)
                {
                    for (int i = op.NumberOfSides - 1; i >= 0; i--)
                    {
                        var vertex = vertices[i];
                        addLine($"{g1} X{vertex.x.ToString(fmt, culture)} Y{vertex.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                    addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }
                else
                {
                    for (int i = 1; i < op.NumberOfSides; i++)
                    {
                        var vertex = vertices[i];
                        addLine($"{g1} X{vertex.x.ToString(fmt, culture)} Y{vertex.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                    addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
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

