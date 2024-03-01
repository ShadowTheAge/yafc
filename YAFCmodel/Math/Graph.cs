using System;
using System.Collections;
using System.Collections.Generic;

namespace YAFC.Model {
    public class Graph<T> : IEnumerable<Graph<T>.Node> {
        private readonly Dictionary<T, Node> nodes = new Dictionary<T, Node>();
        private readonly List<Node> allNodes = new List<Node>();

        private Node GetNode(T src) {
            if (nodes.TryGetValue(src, out var node)) {
                return node;
            }

            return nodes[src] = new Node(this, src);
        }

        public void Connect(T from, T to) {
            GetNode(from).AddArc(GetNode(to));
        }

        public bool HasConnection(T from, T to) {
            return GetNode(from).HasConnection(GetNode(to));
        }

        public ArraySegment<Node> GetConnections(T from) {
            return GetNode(from).Connections;
        }

        public List<Node>.Enumerator GetEnumerator() {
            return allNodes.GetEnumerator();
        }

        IEnumerator<Node> IEnumerable<Node>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public class Node {
            public readonly T userdata;
            public readonly Graph<T> graph;
            public readonly int id;
            internal int state;
            internal int extra;
            private int arccount;
            private Node[] arcs = Array.Empty<Node>();
            public Node(Graph<T> graph, T userdata) {
                this.userdata = userdata;
                this.graph = graph;
                id = graph.allNodes.Count;
                graph.allNodes.Add(this);
            }

            public void AddArc(Node node) {
                if (Array.IndexOf(arcs, node, 0, arccount) != -1) {
                    return;
                }

                if (arccount == arcs.Length) {
                    Array.Resize(ref arcs, Math.Max(arcs.Length * 2, 4));
                }

                arcs[arccount++] = node;
            }

            public ArraySegment<Node> Connections => new ArraySegment<Node>(arcs, 0, arccount);

            public bool HasConnection(Node node) {
                return Array.IndexOf(arcs, node, 0, arccount) >= 0;
            }
        }

        public Graph<TMap> Remap<TMap>(Dictionary<T, TMap> mapping) {
            Graph<TMap> remapped = new Graph<TMap>();
            foreach (var node in allNodes) {
                var remappedNode = mapping[node.userdata];
                foreach (var connection in node.Connections) {
                    remapped.Connect(remappedNode, mapping[connection.userdata]);
                }
            }

            return remapped;
        }

        public Dictionary<T, TValue> Aggregate<TValue>(Func<T, TValue> create, Action<TValue, T, TValue> connection) {
            Dictionary<T, TValue> aggregation = new Dictionary<T, TValue>();
            foreach (var node in allNodes) {
                _ = AggregateInternal(node, create, connection, aggregation);
            }

            return aggregation;
        }

        private TValue AggregateInternal<TValue>(Node node, Func<T, TValue> create, Action<TValue, T, TValue> connection, Dictionary<T, TValue> dict) {
            if (dict.TryGetValue(node.userdata, out var result)) {
                return result;
            }

            result = create(node.userdata);
            dict[node.userdata] = result;
            foreach (var con in node.Connections) {
                connection(result, con.userdata, AggregateInternal(con, create, connection, dict));
            }

            return result;
        }

        public Graph<(T single, T[] list)> MergeStrongConnectedComponents() {
            foreach (var node in allNodes) {
                node.state = -1;
            }

            Dictionary<T, (T, T[])> remap = new Dictionary<T, (T, T[])>();
            List<Node> stack = new List<Node>();
            int index = 0;
            foreach (var node in allNodes) {
                if (node.state == -1) {
                    StrongConnect(stack, node, remap, ref index);
                }
            }

            return Remap(remap);
        }

        private void StrongConnect(List<Node> stack, Node root, Dictionary<T, (T, T[])> remap, ref int index) {
            // Algorithm from https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm
            // index => state
            // lowlink => extra
            // index is undefined => state == -1
            // notOnStack => state = -2
            // v => root
            // w => neighoour
            root.extra = root.state = index++;
            stack.Add(root);
            foreach (var neighbour in root.Connections) {
                if (neighbour.state == -1) {
                    StrongConnect(stack, neighbour, remap, ref index);
                    root.extra = Math.Min(root.extra, neighbour.extra);
                }
                else if (neighbour.state >= 0) {
                    root.extra = Math.Min(root.extra, neighbour.state);
                }
            }

            if (root.extra == root.state) {
                int rootIndex = stack.LastIndexOf(root);
                int count = stack.Count - rootIndex;
                if (count == 1 && !root.HasConnection(root)) {
                    remap[root.userdata] = (root.userdata, null);
                }
                else {
                    T[] range = new T[count];
                    for (int i = 0; i < count; i++) {
                        var userdata = stack[rootIndex + i].userdata;
                        range[i] = userdata;
                        remap[userdata] = (default, range);
                    }
                }

                for (int i = stack.Count - 1; i >= rootIndex; i--) {
                    stack[i].state = -2;
                }

                stack.RemoveRange(rootIndex, count);
            }
        }
    }
}
