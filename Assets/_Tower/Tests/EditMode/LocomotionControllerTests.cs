using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class LocomotionControllerTests
    {
        private const string ControllerPath = "Assets/_Tower/Art/Characters/Animations/PC_Locomotion.controller";
        private const string SpeedParameter = "Speed";

        [Test]
        public void PlayerLocomotionController_DeclaresSpeedBlendTreeContract()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.parameters, Has.Length.GreaterThanOrEqualTo(1));
            Assert.That(controller.parameters, Has.Exactly(1)
                .Matches<AnimatorControllerParameter>(parameter =>
                    parameter.name == SpeedParameter &&
                    parameter.type == AnimatorControllerParameterType.Float));
            Assert.That(controller.layers, Has.Length.GreaterThanOrEqualTo(1));

            AnimatorState defaultState = controller.layers[0].stateMachine.defaultState;
            Assert.That(defaultState, Is.Not.Null);
            Assert.That(defaultState.motion, Is.TypeOf<BlendTree>());

            var tree = (BlendTree)defaultState.motion;
            Assert.That(tree.blendParameter, Is.EqualTo(SpeedParameter));
            Assert.That(tree.children, Has.Length.GreaterThanOrEqualTo(2));

            float previousThreshold = float.NegativeInfinity;
            foreach (ChildMotion child in tree.children)
            {
                Assert.That(child.motion, Is.Not.Null);
                Assert.That(child.threshold, Is.GreaterThanOrEqualTo(previousThreshold));
                previousThreshold = child.threshold;
            }
        }
    }
}
