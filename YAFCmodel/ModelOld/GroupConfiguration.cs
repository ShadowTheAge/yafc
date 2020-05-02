using System;
using System.Collections.Generic;
using YAFC.UI;

namespace YAFC.Model
{
    public class GroupConfiguration : CollectionConfiguration
    {
        private readonly GroupConfiguration parent;
        public GroupConfiguration(GroupConfiguration parent)
        {
            this.parent = parent;
        }

        private List<NodeConfiguration> nodes = new List<NodeConfiguration>();
        private List<GroupConfiguration> groups = new List<GroupConfiguration>();
        public IReadOnlyList<NodeConfiguration> allNodes => nodes;
        public event Action<NodeConfiguration> NewNode;
        public event Action<GroupConfiguration> NewGroup;

        internal override CollectionConfiguration owner => parent;
        internal override object CreateUndoSnapshot() => null;
        internal override void RevertToUndoSnapshot(object snapshot) {}

        internal void AddNode(NodeConfiguration node)
        {
            
        }

        internal void RemoveNode(NodeConfiguration node)
        {
            nodes.Remove(node);
        }

        internal override void SpawnChild(Configuration child, object spawnParameters)
        {
            if (child is NodeConfiguration node)
            {
                nodes.Add(node);
                NewNode?.Invoke(node);
            } 
            else if (child is GroupConfiguration group)
            {
                groups.Add(group);
                NewGroup?.Invoke(group);
                
            }
        }

        internal override object UnspawnChild(Configuration child)
        {
            if (child is NodeConfiguration node)
            {
                nodes.Remove(node);
            }
            else if (child is GroupConfiguration group)
            {
                groups.Remove(group);
            }

            return null;
        }
    }
}