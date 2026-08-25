using System.Windows.Media.Media3D;
using GCodeGenerator.Views.Scene;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Камера трёхмерного окна траектории.
    ///
    /// Орбита, панорама и приближение жили в коде окна вместе с обработкой
    /// мыши, поэтому проверялись только руками — вращением сцены на
    /// запущенной программе. Здесь проверяются свойства, которые пользователь
    /// замечает сразу: сцена не переворачивается на полюсе, вращение не
    /// меняет расстояние, панорама двигает всё вместе.
    /// </summary>
    [TestClass]
    public class OrbitCameraTests
    {
        private const double Tolerance = 1e-9;

        private static OrbitCamera CameraAt(Point3D target, double distance)
        {
            var camera = new OrbitCamera();
            camera.LookAt(target, distance);
            return camera;
        }

        [TestMethod]
        public void LookAt_PutsCameraAboveTarget()
        {
            var camera = CameraAt(new Point3D(10, 20, -5), 50);

            Assert.AreEqual(new Point3D(10, 20, 45), camera.Position);
            Assert.AreEqual(new Vector3D(0, 0, -50), camera.LookDirection);
            Assert.AreEqual(50.0, camera.Distance, Tolerance);
        }

        /// <summary>
        /// Вращение меняет только направление, с которого смотрят: расстояние
        /// до точки вращения остаётся прежним, иначе сцена «подъезжала» бы
        /// при каждом движении мыши.
        /// </summary>
        [TestMethod]
        public void Orbit_KeepsDistanceToPivot()
        {
            var camera = CameraAt(new Point3D(0, 0, 0), 100);
            var before = (camera.Position - camera.Target).Length;

            camera.Orbit(40, 25, camera.Target);

            Assert.AreEqual(before, (camera.Position - camera.Target).Length, 1e-6);
        }

        /// <summary>
        /// Взгляд всегда направлен в точку вращения: иначе после поворота
        /// сцена уезжала бы из кадра.
        /// </summary>
        [TestMethod]
        public void Orbit_KeepsLookingAtPivot()
        {
            var camera = CameraAt(new Point3D(3, 4, 5), 60);
            var pivot = new Point3D(1, 2, 3);

            camera.Orbit(-30, 15, pivot);

            var expected = pivot - camera.Position;
            Assert.AreEqual(expected.X, camera.LookDirection.X, 1e-6);
            Assert.AreEqual(expected.Y, camera.LookDirection.Y, 1e-6);
            Assert.AreEqual(expected.Z, camera.LookDirection.Z, 1e-6);
        }

        /// <summary>
        /// Наклон упирается в пределы, не доходя до полюсов: там «верх» кадра
        /// неопределён, и сцена скачком переворачивается вверх ногами.
        /// </summary>
        [TestMethod]
        public void Orbit_DoesNotFlipOverThePole()
        {
            var camera = CameraAt(new Point3D(0, 0, 0), 100);

            // Тянем мышь вверх заведомо дальше, чем на пол-оборота.
            for (var i = 0; i < 100; i++)
                camera.Orbit(0, -100, camera.Target);

            Assert.IsTrue(camera.Position.Z < 0, "камера ушла под модель, но не перевернулась");
            AssertPerpendicular(camera);

            for (var i = 0; i < 200; i++)
                camera.Orbit(0, 100, camera.Target);

            Assert.IsTrue(camera.Position.Z > 0, "и вернулась наверх");
            AssertPerpendicular(camera);
        }

        /// <summary>
        /// «Верх» кадра перпендикулярен взгляду при любом положении камеры:
        /// иначе изображение перекашивается.
        /// </summary>
        [TestMethod]
        public void Orbit_KeepsUpPerpendicularToView()
        {
            var camera = CameraAt(new Point3D(0, 0, 0), 100);

            foreach (var (dx, dy) in new[] { (30.0, 10.0), (-70.0, 40.0), (15.0, -90.0), (120.0, 5.0) })
            {
                camera.Orbit(dx, dy, camera.Target);
                AssertPerpendicular(camera);
            }
        }

        [TestMethod]
        public void Zoom_MovesTowardsAndAwayFromTarget()
        {
            var camera = CameraAt(new Point3D(0, 0, 0), 100);

            camera.Zoom(closer: true);
            Assert.IsTrue(camera.Distance < 100, "приближение сокращает расстояние");
            Assert.AreEqual(camera.Distance, (camera.Position - camera.Target).Length, 1e-6);

            var closer = camera.Distance;
            camera.Zoom(closer: false);
            Assert.IsTrue(camera.Distance > closer, "отдаление увеличивает расстояние");
        }

        /// <summary>
        /// Приближение не проходит сквозь цель и не улетает в бесконечность:
        /// и то и другое оставляет пустой кадр, из которого не выбраться.
        /// </summary>
        [TestMethod]
        public void Zoom_StopsAtLimits()
        {
            var near = CameraAt(new Point3D(0, 0, 0), 100);
            for (var i = 0; i < 200; i++)
                near.Zoom(closer: true);

            Assert.IsTrue(near.Distance > 0, "камера не проходит сквозь цель");

            var far = CameraAt(new Point3D(0, 0, 0), 100);
            for (var i = 0; i < 200; i++)
                far.Zoom(closer: false);

            Assert.IsTrue(far.Distance <= 10000, "камера не улетает за предел");
        }

        /// <summary>
        /// Панорама двигает камеру и цель на один и тот же вектор: сцена
        /// смещается в кадре, но остаётся к камере тем же боком.
        /// </summary>
        [TestMethod]
        public void Pan_MovesCameraAndTargetTogether()
        {
            var camera = CameraAt(new Point3D(0, 0, 0), 100);
            camera.Orbit(35, 20, camera.Target);

            var positionBefore = camera.Position;
            var targetBefore = camera.Target;
            var lookBefore = camera.LookDirection;

            camera.Pan(25, -15);

            var positionShift = camera.Position - positionBefore;
            var targetShift = camera.Target - targetBefore;

            Assert.AreEqual(positionShift.X, targetShift.X, 1e-6);
            Assert.AreEqual(positionShift.Y, targetShift.Y, 1e-6);
            Assert.AreEqual(positionShift.Z, targetShift.Z, 1e-6);
            Assert.IsTrue(positionShift.Length > 0, "панорама действительно сдвигает камеру");

            var lookAfter = camera.LookDirection;
            Assert.AreEqual(lookBefore.X, lookAfter.X, 1e-6, "направление взгляда не меняется");
            Assert.AreEqual(lookBefore.Y, lookAfter.Y, 1e-6);
            Assert.AreEqual(lookBefore.Z, lookAfter.Z, 1e-6);
        }

        /// <summary>
        /// Шаг панорамы растёт с расстоянием: на общем плане сцена должна
        /// двигаться за курсором так же охотно, как на крупном.
        /// </summary>
        [TestMethod]
        public void Pan_ScalesWithDistance()
        {
            var near = CameraAt(new Point3D(0, 0, 0), 10);
            var far = CameraAt(new Point3D(0, 0, 0), 1000);

            var nearBefore = near.Target;
            var farBefore = far.Target;

            near.Pan(50, 0);
            far.Pan(50, 0);

            Assert.IsTrue((far.Target - farBefore).Length > (near.Target - nearBefore).Length,
                "дальняя камера смещается сильнее");
        }

        private static void AssertPerpendicular(OrbitCamera camera)
        {
            var look = camera.LookDirection;
            var up = camera.UpDirection;
            look.Normalize();
            up.Normalize();

            Assert.AreEqual(0.0, Vector3D.DotProduct(look, up), 1e-6,
                "«верх» кадра должен быть перпендикулярен взгляду");
        }
    }
}
