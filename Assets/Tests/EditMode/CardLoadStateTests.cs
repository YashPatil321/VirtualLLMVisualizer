using NUnit.Framework;
using OCS.VR.Rig;

namespace OCS.VR.Tests
{
    /// <summary>
    /// Covers the Act 5 card lighting rule. CardLoadState is plain C#, so this needs no
    /// scene and no headset.
    /// </summary>
    public class CardLoadStateTests
    {
        [Test]
        public void StartingAHopLightsThatCard()
        {
            var s = new CardLoadState(8);
            s.HopStarted(3, 0.94f);

            Assert.AreEqual(3, s.ActiveCard);
            Assert.AreEqual(0.94f, s.Target(3));
        }

        [Test]
        public void EndingTheOnlyHopDarkensTheCard()
        {
            var s = new CardLoadState(8);
            s.HopStarted(3, 0.94f);
            s.HopEnded(3);

            Assert.AreEqual(0f, s.Target(3));
            Assert.AreEqual(-1, s.ActiveCard);
        }

        [Test]
        public void AdjacentHopsOnOneCardDoNotBlankIt()
        {
            // The real failure this guards. In sample-trace.json the rig hop on card 3
            // ends at 82ms and the model hop on card 3 starts at 82ms, so both land in
            // one frame: the model hop starts, then the rig hop ends. Last event wins
            // would leave card 3 dark for the whole three seconds of generation, which is
            // the one moment the experience exists to show.
            var s = new CardLoadState(8);

            s.HopStarted(3, 0.94f);   // rig hop, 55-82ms
            s.HopStarted(3, 1.00f);   // model hop starts, 82-3154ms
            s.HopEnded(3);            // rig hop ends, same frame

            Assert.AreEqual(1, s.ActiveHopCount(3), "model hop should still be running");
            Assert.AreEqual(1.00f, s.Target(3), "card 3 must stay lit through generation");
            Assert.AreEqual(3, s.ActiveCard);

            s.HopEnded(3);            // model hop ends, 3154ms
            Assert.AreEqual(0f, s.Target(3));
            Assert.AreEqual(-1, s.ActiveCard);
        }

        [Test]
        public void UnbalancedEndsDoNotDriveTheCountNegative()
        {
            var s = new CardLoadState(8);
            s.HopEnded(3);
            s.HopEnded(3);
            s.HopStarted(3, 1f);

            Assert.AreEqual(1, s.ActiveHopCount(3));
            s.HopEnded(3);
            Assert.AreEqual(0f, s.Target(3));
        }

        [Test]
        public void OutOfRangeCardsAreIgnored()
        {
            var s = new CardLoadState(8);
            s.HopStarted(99, 1f);
            s.HopStarted(-1, 1f);

            Assert.AreEqual(-1, s.ActiveCard);
            Assert.AreEqual(0f, s.Current(99));
        }

        [Test]
        public void StepEasesCurrentTowardTarget()
        {
            var s = new CardLoadState(8);
            s.HopStarted(3, 1f);

            Assert.AreEqual(0f, s.Current(3));
            for (int i = 0; i < 72; i++) s.Step(1f / 72f, 6f);

            Assert.IsTrue(s.Current(3) > 0.9f, "should be near fully lit after a second");
        }

        [Test]
        public void ResetAllClearsEverything()
        {
            var s = new CardLoadState(8);
            s.HopStarted(3, 1f);
            s.HopStarted(5, 1f);
            s.ResetAll();

            Assert.AreEqual(0f, s.Target(3));
            Assert.AreEqual(0f, s.Target(5));
            Assert.AreEqual(0, s.ActiveHopCount(3));
            Assert.AreEqual(-1, s.ActiveCard);
        }
    }
}
