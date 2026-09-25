using NUnit.Framework;
using OCS.VR.Rig;

namespace OCS.VR.Tests
{
    public class CardVisualMathTests
    {
        [Test]
        public void UnpoweredCardIsDarkAndStill()
        {
            // Act 1 and 2 happen before power on. A card that glows on an empty bench
            // gives the ending away.
            Assert.AreEqual(0f, CardVisualMath.Glow(false, 1f, 0.12f));
            Assert.AreEqual(0f, CardVisualMath.FanRps(false, 1f, 4f, 22f));
        }

        [Test]
        public void PoweredIdleCardGlowsFaintlyAndIdles()
        {
            Assert.AreEqual(0.12f, CardVisualMath.Glow(true, 0f, 0.12f));
            Assert.AreEqual(4f, CardVisualMath.FanRps(true, 0f, 4f, 22f));
        }

        [Test]
        public void FullLoadIsFullyLitAndFastest()
        {
            Assert.AreEqual(1f, CardVisualMath.Glow(true, 1f, 0.12f));
            Assert.AreEqual(22f, CardVisualMath.FanRps(true, 1f, 4f, 22f));
        }

        [Test]
        public void LoadOutsideZeroToOneIsClamped()
        {
            Assert.AreEqual(1f, CardVisualMath.Glow(true, 5f, 0.12f));
            Assert.AreEqual(4f, CardVisualMath.FanRps(true, -1f, 4f, 22f));
        }

        [Test]
        public void ApproachMovesByAtMostTheStepAndNeverOvershoots()
        {
            Assert.AreEqual(1f, CardVisualMath.Approach(0f, 10f, 1f));
            Assert.AreEqual(10f, CardVisualMath.Approach(9.5f, 10f, 1f));
            Assert.AreEqual(9f, CardVisualMath.Approach(10f, 0f, 1f));
            Assert.AreEqual(0f, CardVisualMath.Approach(0.5f, 0f, 1f));
            Assert.AreEqual(3f, CardVisualMath.Approach(3f, 10f, 0f));
        }

        [Test]
        public void FansSpinUpGraduallyAfterPowerOn()
        {
            // Stepped at 72 fps, fans should take visible time to reach idle speed.
            float rps = 0f;
            float target = CardVisualMath.FanRps(true, 0f, 4f, 22f);
            int frames = 0;
            while (rps < target && frames < 1000)
            {
                rps = CardVisualMath.Approach(rps, target, 8f / 72f);
                frames++;
            }
            Assert.AreEqual(target, rps);
            Assert.IsTrue(frames > 30, "spin up should take around half a second, not one frame");
        }
    }
}
