using System;
using System.Collections.Generic;
using UnityEngine;

namespace SunriseStudio.UI
{
    [CreateAssetMenu(
        fileName = "Dialogue",
        menuName = "Sunrise Studio/UI Data/Dialogue")]
    public sealed class DialogueData : ScriptableObject
    {
        [SerializeField] string firstNodeId;
        [SerializeField] List<DialogueNode> nodes = new();

        public string FirstNodeId => firstNodeId;
        public IReadOnlyList<DialogueNode> Nodes => nodes;

        public bool TryGetNode(string nodeId, out DialogueNode node)
        {
            node = nodes.Find(candidate => candidate.Id == nodeId);
            return node != null;
        }
    }

    [Serializable]
    public sealed class DialogueNode
    {
        [SerializeField] string id;
        [SerializeField] string speaker;
        [TextArea(2, 8)] [SerializeField] string body;
        [SerializeField] string nextNodeId;
        [SerializeField] List<DialogueChoice> choices = new();

        public string Id => id;
        public string Speaker => speaker;
        public string Body => body;
        public string NextNodeId => nextNodeId;
        public IReadOnlyList<DialogueChoice> Choices => choices;
    }

    [Serializable]
    public sealed class DialogueChoice
    {
        [SerializeField] string id;
        [TextArea(1, 3)] [SerializeField] string label;
        [SerializeField] string nextNodeId;

        public string Id => id;
        public string Label => label;
        public string NextNodeId => nextNodeId;
    }
}
