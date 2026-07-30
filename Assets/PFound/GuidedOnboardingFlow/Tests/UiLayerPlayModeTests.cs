using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PFound.GuidedOnboardingFlow.Tests
{
    /// <summary>PlayMode coverage of the real MonoBehaviour UI + a time-driven manager run.</summary>
    public sealed class UiLayerPlayModeTests
    {
        [Test]
        public void TutorialCanvas_SetVisibleTogglesCanvas()
        {
            var go = new GameObject("canvas", typeof(Canvas), typeof(TutorialCanvas));
            var controller = go.GetComponent<TutorialCanvas>();

            controller.SetVisible(false);
            Assert.IsFalse(controller.Canvas.enabled, "hidden");
            controller.SetVisible(true);
            Assert.IsTrue(controller.Canvas.enabled, "shown");

            Object.Destroy(go);
        }

        [Test]
        public void DialogPanel_ContinueButtonLatchesConsumeContinue()
        {
            var panel = new GameObject("dialog").AddComponent<DialogPanel>();
            var button = new GameObject("btn", typeof(RectTransform)).AddComponent<Button>();
            SetPrivate(panel, "_continueButton", button);

            // Re-run Awake wiring now that the button is assigned.
            typeof(DialogPanel).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(panel, null);

            panel.Show("hi");
            Assert.IsFalse(panel.ConsumeContinue(), "not continued yet");
            button.onClick.Invoke();
            Assert.IsTrue(panel.ConsumeContinue(), "latched after click");
            Assert.IsFalse(panel.ConsumeContinue(), "one-shot");

            Object.Destroy(panel.gameObject);
            Object.Destroy(button.gameObject);
        }

        [UnityTest]
        public IEnumerator Manager_DrivesADelayedRunToCompletion_AndPersists()
        {
            var store = new InMemoryCompletionStore();
            var log = new UnityTutorialLog();
            var id = new TutorialId(42);

            var blueprint = new TutorialBlueprint(
                id, "Sample", TriggerMode.Manual,
                () => new List<ITutorialStep> { new DelayStep(0.2f), new LogStep(log, "done") });

            var manager = new TutorialManager(new[] { blueprint }, store);
            TutorialOutcome? outcome = null;
            manager.TutorialEnded += (_, o) => outcome = o;

            Assert.IsTrue(manager.TryStart(id), "started");

            float t = 0f;
            while (manager.IsRunning && t < 2f)
            {
                manager.Tick(Time.deltaTime);
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(TutorialOutcome.Completed, outcome, "completed");
            Assert.IsTrue(store.IsCompleted(id), "persisted completion");
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo fi = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            fi.SetValue(target, value);
        }
    }
}
