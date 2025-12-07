using System;
using System.Globalization;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class ProfileRectangleOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings)
        {
            if (!(operation is ProfileRectangleOperation op))
                return;

            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var toolRadius = op.ToolDiameter / 2.0;
            var offset = 0.0;
            if (op.ToolPathMode == ToolPathMode.Outside)
                offset = toolRadius;
            else if (op.ToolPathMode == ToolPathMode.Inside)
                offset = -toolRadius;

            double centerX, centerY;
            switch (op.ReferencePointType)
            {
                case ReferencePointType.Center:
                    centerX = op.ReferencePointX;
                    centerY = op.ReferencePointY;
                    break;
                case ReferencePointType.TopLeft:
                    centerX = op.ReferencePointX + op.Width / 2.0;
                    centerY = op.ReferencePointY - op.Height / 2.0;
                    break;
                case ReferencePointType.TopRight:
                    centerX = op.ReferencePointX - op.Width / 2.0;
                    centerY = op.ReferencePointY - op.Height / 2.0;
                    break;
                case ReferencePointType.BottomLeft:
                    centerX = op.ReferencePointX + op.Width / 2.0;
                    centerY = op.ReferencePointY + op.Height / 2.0;
                    break;
                case ReferencePointType.BottomRight:
                    centerX = op.ReferencePointX - op.Width / 2.0;
                    centerY = op.ReferencePointY + op.Height / 2.0;
                    break;
                default:
                    centerX = op.ReferencePointX;
                    centerY = op.ReferencePointY;
                    break;
            }

            var halfWidth = op.Width / 2.0 + offset;
            var halfHeight = op.Height / 2.0 + offset;

            var corners = new[]
            {
                new { X = -halfWidth, Y = -halfHeight },
                new { X = halfWidth, Y = -halfHeight },
                new { X = halfWidth, Y = halfHeight },
                new { X = -halfWidth, Y = halfHeight }
            };

            var angleRad = op.RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            var rotatedCorners = corners.Select(c =>
            {
                var x = c.X * cos - c.Y * sin;
                var y = c.X * sin + c.Y * cos;
                return new { X = centerX + x, Y = centerY + y };
            }).ToArray();

            int[] cornerOrder;
            if (op.Direction == MillingDirection.Clockwise)
            {
                cornerOrder = new[] { 0, 3, 2, 1 };
            }
            else
            {
                cornerOrder = new[] { 0, 1, 2, 3 };
            }

            var currentZ = op.ContourHeight;
            var finalZ = op.ContourHeight - op.TotalDepth;
            var passNumber = 0;

            while (currentZ > finalZ)
            {
                var nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ)
                    nextZ = finalZ;

                passNumber++;

                if (settings.UseComments)
                    addLine($"(Pass {passNumber}, depth {nextZ.ToString(fmt, culture)})");

                var startCorner = rotatedCorners[cornerOrder[0]];
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{startCorner.X.ToString(fmt, culture)} Y{startCorner.Y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

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
                    
                    var rampStartZ = retractZ;
                    var rampEndZ = nextZ;
                    var tanAngle = Math.Tan(entryAngleRad);
                    
                    var rampCurrentZ = rampStartZ;
                    var rampCurrentX = startCorner.X;
                    var rampCurrentY = startCorner.Y;
                    var totalDistanceTraveled = 0.0;
                    var edgeIndex = 0;
                    var rampComplete = false;
                    
                    while (!rampComplete && edgeIndex < cornerOrder.Length)
                    {
                        var edgeStart = rotatedCorners[cornerOrder[edgeIndex]];
                        var edgeEnd = rotatedCorners[cornerOrder[(edgeIndex + 1) % cornerOrder.Length]];
                        var edgeDx = edgeEnd.X - edgeStart.X;
                        var edgeDy = edgeEnd.Y - edgeStart.Y;
                        var edgeLength = Math.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
                        
                        if (edgeLength > 0.001)
                        {
                            var edgeDirX = edgeDx / edgeLength;
                            var edgeDirY = edgeDy / edgeLength;
                            
                            var remainingDepth = rampStartZ - rampEndZ - totalDistanceTraveled * tanAngle;
                            
                            if (remainingDepth <= 0)
                            {
                                rampComplete = true;
                                break;
                            }
                            
                            var maxDistanceOnEdge = remainingDepth / tanAngle;
                            
                            if (maxDistanceOnEdge >= edgeLength)
                            {
                                var newZ = rampStartZ - (totalDistanceTraveled + edgeLength) * tanAngle;
                                if (newZ < rampEndZ) newZ = rampEndZ;
                                
                                addLine($"{g1} X{edgeEnd.X.ToString(fmt, culture)} Y{edgeEnd.Y.ToString(fmt, culture)} Z{newZ.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                                
                                rampCurrentX = edgeEnd.X;
                                rampCurrentY = edgeEnd.Y;
                                rampCurrentZ = newZ;
                                totalDistanceTraveled += edgeLength;
                                
                                if (Math.Abs(newZ - rampEndZ) < 0.001)
                                {
                                    rampComplete = true;
                                    break;
                                }
                                
                                edgeIndex++;
                            }
                            else
                            {
                                var finalX = edgeStart.X + edgeDirX * maxDistanceOnEdge;
                                var finalY = edgeStart.Y + edgeDirY * maxDistanceOnEdge;
                                
                                addLine($"{g1} X{finalX.ToString(fmt, culture)} Y{finalY.ToString(fmt, culture)} Z{rampEndZ.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                                
                                rampCurrentX = finalX;
                                rampCurrentY = finalY;
                                rampCurrentZ = rampEndZ;
                                rampComplete = true;
                                break;
                            }
                        }
                        else
                        {
                            edgeIndex++;
                        }
                    }
                    
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{startCorner.X.ToString(fmt, culture)} Y{startCorner.Y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                    
                    addLine($"{g0} Z{nextZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    
                    for (int i = 1; i < cornerOrder.Length; i++)
                    {
                        var corner = rotatedCorners[cornerOrder[i]];
                        addLine($"{g1} X{corner.X.ToString(fmt, culture)} Y{corner.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                    
                    addLine($"{g1} X{startCorner.X.ToString(fmt, culture)} Y{startCorner.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }

                if (op.EntryMode == EntryMode.Vertical)
                {
                    for (int i = 1; i < cornerOrder.Length; i++)
                    {
                        var corner = rotatedCorners[cornerOrder[i]];
                        addLine($"{g1} X{corner.X.ToString(fmt, culture)} Y{corner.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                    
                    addLine($"{g1} X{startCorner.X.ToString(fmt, culture)} Y{startCorner.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
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

