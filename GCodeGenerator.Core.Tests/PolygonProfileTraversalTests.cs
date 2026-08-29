#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Регрессии порядка обхода многоугольного профиля.</summary>
    [TestClass]
    public sealed class PolygonProfileTraversalTests
    {
        [TestMethod]
        public void ClockwiseSquare_CutsEachEdgeExactlyOnce()
        {
            var operation = OperationFixtures.ProfilePolygon();
            operation.NumberOfSides = 4;
            operation.Radius = 10;
            operation.Direction = MillingDirection.Clockwise;
            operation.ToolPathMode = ToolPathMode.OnLine;
            operation.TotalDepth = 1;
            operation.StepDepth = 1;

            var path = OperationToolPath.Build(
                new UnifiedProfileGenerator(),
                operation,
                new GCodeSettings());
            var edges = NonZeroWorkingEdges(path).ToList();

            Assert.AreEqual(4, edges.Count, "Квадрат должен дать четыре ненулевых ребра");
            Assert.AreEqual(4, edges.Select(CanonicalEdge).Distinct().Count(),
                "Ни одно ребро не должно проходиться повторно");
        }

        private static IEnumerable<((double X, double Y) From, (double X, double Y) To)> NonZeroWorkingEdges(
            ToolPath path)
        {
            var x = 0.0;
            var y = 0.0;
            var z = 0.0;

            foreach (var move in path.Moves())
            {
                var from = (X: x, Y: y);
                x = move.X ?? x;
                y = move.Y ?? y;
                z = move.Z ?? z;

                if (move.Kind == ToolMoveKind.Linear
                    && Math.Abs(z + 1.0) <= 1e-9
                    && (move.X.HasValue || move.Y.HasValue)
                    && Math.Sqrt(Math.Pow(x - from.X, 2) + Math.Pow(y - from.Y, 2)) > 1e-9)
                {
                    yield return (from, (x, y));
                }
            }
        }

        private static string CanonicalEdge(((double X, double Y) From, (double X, double Y) To) edge)
        {
            var first = FormattableString.Invariant($"{edge.From.X:0.000000},{edge.From.Y:0.000000}");
            var second = FormattableString.Invariant($"{edge.To.X:0.000000},{edge.To.Y:0.000000}");
            return string.CompareOrdinal(first, second) <= 0
                ? first + "|" + second
                : second + "|" + first;
        }
    }
}
