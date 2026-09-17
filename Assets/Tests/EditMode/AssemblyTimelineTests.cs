using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OCS.VR.Rig;

namespace OCS.VR.Tests
{
    public class AssemblyTimelineTests
    {
        static AssemblySequence MakeSequence(int count = 3, float travel = 1f, float settle = 0.5f, float gap = 0.25f)
        {
            var seq = ScriptableObject.CreateInstance<AssemblySequence>();
            seq.gapSeconds = gap;
            seq.steps = new List<AssemblyStep>();
            for (int i = 0; i < count; i++)
            {
                seq.steps.Add(new AssemblyStep
                {
                    stepId = "step" + i,
                    partName = "Part " + i,
                    travelSeconds = travel,
                    settleSeconds = settle
                });
            }
            return seq;
        }

        [Test]
        public void TotalDurationCountsStepsAndGaps()
        {
            var seq = MakeSequence(3, 1f, 0.5f, 0.25f);
            // 3 steps of 1.5s, plus 2 gaps of 0.25s
            Assert.AreEqual(5.0f, seq.TotalDurationSeconds);
        }

        [Test]
        public void StepsRunInOrder()
        {
            var seq = MakeSequence(3);
            var t = new AssemblyTimeline(seq);

            var started = new List<string>();
            t.StepStarted += (s, i) => started.Add(s.stepId);

            for (int f = 0; f < 600; f++) t.Advance(1f / 72f);

            Assert.AreEqual(3, started.Count);
            Assert.AreEqual("step0", started[0]);
            Assert.AreEqual("step1", started[1]);
            Assert.AreEqual("step2", started[2]);
        }

        [Test]
        public void PartStaysSeatedDuringTheGapBetweenSteps()
        {
            // The gap is the quiet beat after a part settles. Reporting travel progress 0
            // there would snap the part back to the tray for a quarter of a second.
            var seq = MakeSequence(2, travel: 1f, settle: 0.5f, gap: 0.25f);
            var t = new AssemblyTimeline(seq);

            // Step 0 spans 0 to 1.5s, then a gap until 1.75s.
            while (t.Elapsed < 1.6f) t.Advance(1f / 72f);

            Assert.AreEqual(0, t.CurrentIndex, "still reporting the step that just seated");
            Assert.AreEqual(1f, t.TravelProgress01, "the seated part must not travel backwards");
        }

        [Test]
        public void TravelProgressRunsZeroToOneAcrossTheTravel()
        {
            var seq = MakeSequence(1, travel: 1f, settle: 0f, gap: 0f);
            var t = new AssemblyTimeline(seq);

            t.Advance(0.001f);
            Assert.IsTrue(t.TravelProgress01 < 0.05f);

            while (t.Elapsed < 0.5f) t.Advance(1f / 72f);
            Assert.IsTrue(t.TravelProgress01 > 0.4f && t.TravelProgress01 < 0.6f, "about halfway");

            while (!t.IsFinished) t.Advance(1f / 72f);
            Assert.AreEqual(1f, t.TravelProgress01);
        }

        [Test]
        public void FinishesAfterTheLastStep()
        {
            var seq = MakeSequence(2);
            var t = new AssemblyTimeline(seq);

            int finished = 0;
            t.Finished += () => finished++;

            for (int f = 0; f < 600; f++) t.Advance(1f / 72f);

            Assert.IsTrue(t.IsFinished);
            Assert.AreEqual(1, finished, "Finished must raise exactly once");
        }

        [Test]
        public void ALongFrameCanCrossSeveralSteps()
        {
            // A hitch on device must not leave steps un-started.
            var seq = MakeSequence(3);
            var t = new AssemblyTimeline(seq);

            var started = new List<string>();
            t.StepStarted += (s, i) => started.Add(s.stepId);

            t.Advance(10f);

            Assert.AreEqual(3, started.Count, "every step should still have fired");
            Assert.IsTrue(t.IsFinished);
        }

        [Test]
        public void EmptySequenceFinishesInsteadOfHanging()
        {
            var seq = ScriptableObject.CreateInstance<AssemblySequence>();
            seq.steps = new List<AssemblyStep>();
            var t = new AssemblyTimeline(seq);

            int finished = 0;
            t.Finished += () => finished++;
            t.Advance(1f / 72f);

            Assert.IsTrue(t.IsFinished, "the arc would wait forever otherwise");
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void ValidateRejectsDuplicateStepIds()
        {
            var seq = MakeSequence(2);
            seq.steps[1].stepId = seq.steps[0].stepId;

            string error;
            Assert.IsFalse(seq.Validate(out error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void ValidateRejectsZeroTravel()
        {
            var seq = MakeSequence(1);
            seq.steps[0].travelSeconds = 0f;

            string error;
            Assert.IsFalse(seq.Validate(out error));
        }

        [Test]
        public void ValidateAcceptsAWellFormedSequence()
        {
            string error;
            Assert.IsTrue(MakeSequence(3).Validate(out error), error);
        }
    }
}
