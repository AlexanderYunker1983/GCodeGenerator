using System;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class ProfileCircleOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings)
        {
            if (!(operation is ProfileCircleOperation op))
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

            var startX = op.CenterX + actualRadius;
            var startY = op.CenterY;

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
                    
                    var circumference = 2 * Math.PI * actualRadius;
                    var angleForRamp = (rampDistance / circumference) * 2 * Math.PI;
                    
                    var rampStartAngle = 0.0;
                    var rampEndAngle = angleForRamp;
                    if (op.Direction == MillingDirection.Clockwise)
                        rampEndAngle = -angleForRamp;
                    
                    var rampSegments = Math.Max(4, (int)(Math.Abs(angleForRamp) / (Math.PI / 16)));
                    for (int i = 1; i <= rampSegments; i++)
                    {
                        var t = (double)i / rampSegments;
                        var angle = rampStartAngle + t * rampEndAngle;
                        var x = op.CenterX + actualRadius * Math.Cos(angle);
                        var y = op.CenterY + actualRadius * Math.Sin(angle);
                        var z = retractZ - t * rampDepth;
                        
                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} Z{z.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                    
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{nextZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                if (settings.AllowArcs)
                {
                    var g2 = settings.UsePaddedGCodes ? "G02" : "G2";
                    var g3 = settings.UsePaddedGCodes ? "G03" : "G3";
                    
                    var i = op.CenterX - startX;
                    var j = op.CenterY - startY;
                    
                    var arcCommand = op.Direction == MillingDirection.Clockwise ? g2 : g3;
                    
                    var midX = op.CenterX - actualRadius;
                    var midY = op.CenterY;
                    addLine($"{arcCommand} X{midX.ToString(fmt, culture)} Y{midY.ToString(fmt, culture)} I{i.ToString(fmt, culture)} J{j.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    
                    var i2 = op.CenterX - midX;
                    var j2 = op.CenterY - midY;
                    addLine($"{arcCommand} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} I{i2.ToString(fmt, culture)} J{j2.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }
                else
                {
                    var circumference = 2 * Math.PI * actualRadius;
                    var numSegments = Math.Max(4, (int)Math.Ceiling(circumference / op.MaxSegmentLength));
                    var angleStep = 2 * Math.PI / numSegments;
                    
                    if (op.Direction == MillingDirection.Clockwise)
                        angleStep = -angleStep;
                    
                    for (int i = 1; i <= numSegments; i++)
                    {
                        var angle = i * angleStep;
                        var x = op.CenterX + actualRadius * Math.Cos(angle);
                        var y = op.CenterY + actualRadius * Math.Sin(angle);
                        
                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
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

