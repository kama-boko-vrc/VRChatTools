#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Animations;

/// <summary>
/// Animator Controllerのパラメータ使用状況を走査するための共通ヘルパー。
/// State/Transition（Any State・Entry含む）・BlendTree（Direct含む）を再帰的にたどり、
/// 実際に参照されているパラメータ名を集める。複数のツールから再利用される。
/// </summary>
internal static class AnimatorControllerUtility
{
    /// <summary>
    /// ステートマシン配下（サブステートマシン含む）のTransition条件・Motion Time/Speed/
    /// Cycle Offset/Mirrorパラメータ・BlendTreeのBlend Parameterを再帰的に集める。
    /// </summary>
    public static void CollectUsedParameters(AnimatorStateMachine stateMachine, HashSet<string> used)
    {
        foreach (AnimatorStateTransition t in stateMachine.anyStateTransitions) CollectFromTransition(t, used);
        foreach (AnimatorTransition t in stateMachine.entryTransitions) CollectFromTransition(t, used);

        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            AnimatorState state = childState.state;
            CollectFromState(state, used);
            foreach (AnimatorStateTransition t in state.transitions) CollectFromTransition(t, used);
        }

        foreach (ChildAnimatorStateMachine childSM in stateMachine.stateMachines)
        {
            foreach (AnimatorTransition t in stateMachine.GetStateMachineTransitions(childSM.stateMachine))
            {
                CollectFromTransition(t, used);
            }
            CollectUsedParameters(childSM.stateMachine, used);
        }
    }

    private static void CollectFromTransition(AnimatorTransitionBase transition, HashSet<string> used)
    {
        foreach (AnimatorCondition cond in transition.conditions)
        {
            used.Add(cond.parameter);
        }
    }

    private static void CollectFromState(AnimatorState state, HashSet<string> used)
    {
        if (state.timeParameterActive) used.Add(state.timeParameter);
        if (state.speedParameterActive) used.Add(state.speedParameter);
        if (state.cycleOffsetParameterActive) used.Add(state.cycleOffsetParameter);
        if (state.mirrorParameterActive) used.Add(state.mirrorParameter);

        if (state.motion is BlendTree tree) CollectFromBlendTree(tree, used);
    }

    private static void CollectFromBlendTree(BlendTree tree, HashSet<string> used)
    {
        if (!string.IsNullOrEmpty(tree.blendParameter)) used.Add(tree.blendParameter);
        if (!string.IsNullOrEmpty(tree.blendParameterY)) used.Add(tree.blendParameterY);

        foreach (ChildMotion child in tree.children)
        {
            if (!string.IsNullOrEmpty(child.directBlendParameter)) used.Add(child.directBlendParameter);
            if (child.motion is BlendTree childTree) CollectFromBlendTree(childTree, used);
        }
    }
}
#endif