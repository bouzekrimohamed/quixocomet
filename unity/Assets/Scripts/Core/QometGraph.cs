using System.Collections.Generic;
using UnityEngine;

namespace QuixoUnity.Core
{
    public readonly struct QometNode
    {
        public readonly string Id;
        public readonly int Row;
        public readonly int Col;
        public readonly Vector2 Position;
        public readonly Vector2 VisualPosition;

        public QometNode(string id, int row, int col, Vector2 position, Vector2 visualPosition)
        {
            Id = id;
            Row = row;
            Col = col;
            Position = position;
            VisualPosition = visualPosition;
        }
    }

    public readonly struct QometEdge
    {
        public readonly Vector2Int A;
        public readonly Vector2Int B;

        public QometEdge(Vector2Int a, Vector2Int b)
        {
            A = a;
            B = b;
        }
    }

    public static class QometGraph
    {
        public const int BoardSize = 7;

        private static readonly QometNode[] Nodes =
        {
            new("A", 0, 2, new Vector2(-1f, 3f), new Vector2(-3f, 3f)),
            new("B", 0, 3, new Vector2(0f, 3f), new Vector2(0f, 3f)),
            new("C", 0, 4, new Vector2(1f, 3f), new Vector2(3f, 3f)),
            new("D", 1, 2, new Vector2(-1f, 2f), new Vector2(-2f, 2f)),
            new("E", 1, 3, new Vector2(0f, 2f), new Vector2(0f, 2f)),
            new("F", 1, 4, new Vector2(1f, 2f), new Vector2(2f, 2f)),
            new("G", 2, 2, new Vector2(-1f, 1f), new Vector2(-1f, 1f)),
            new("H", 2, 3, new Vector2(0f, 1f), new Vector2(0f, 1f)),
            new("I", 2, 4, new Vector2(1f, 1f), new Vector2(1f, 1f)),
            new("J", 3, 0, new Vector2(-3f, 0f), new Vector2(-3f, 0f)),
            new("K", 3, 1, new Vector2(-2f, 0f), new Vector2(-2f, 0f)),
            new("L", 3, 2, new Vector2(-1f, 0f), new Vector2(-1f, 0f)),
            new("M", 3, 3, new Vector2(0f, 0f), new Vector2(0f, 0f)),
            new("N", 3, 4, new Vector2(1f, 0f), new Vector2(1f, 0f)),
            new("O", 3, 5, new Vector2(2f, 0f), new Vector2(2f, 0f)),
            new("P", 3, 6, new Vector2(3f, 0f), new Vector2(3f, 0f)),
            new("Q", 4, 2, new Vector2(-1f, -1f), new Vector2(-1f, -1f)),
            new("R", 4, 3, new Vector2(0f, -1f), new Vector2(0f, -1f)),
            new("S", 4, 4, new Vector2(1f, -1f), new Vector2(1f, -1f)),
            new("T", 5, 2, new Vector2(-1f, -2f), new Vector2(-2f, -2f)),
            new("U", 5, 3, new Vector2(0f, -2f), new Vector2(0f, -2f)),
            new("V", 5, 4, new Vector2(1f, -2f), new Vector2(2f, -2f)),
            new("W", 6, 2, new Vector2(-1f, -3f), new Vector2(-3f, -3f)),
            new("X", 6, 3, new Vector2(0f, -3f), new Vector2(0f, -3f)),
            new("Y", 6, 4, new Vector2(1f, -3f), new Vector2(3f, -3f)),
        };

        private static readonly int[,] EdgeNodeIndices =
        {
            { 0, 1 }, { 1, 2 },
            { 3, 4 }, { 4, 5 },
            { 6, 7 }, { 7, 8 },
            { 9, 10 }, { 10, 11 }, { 11, 12 }, { 12, 13 }, { 13, 14 }, { 14, 15 },
            { 16, 17 }, { 17, 18 },
            { 19, 20 }, { 20, 21 },
            { 22, 23 }, { 23, 24 },
            { 1, 4 }, { 4, 7 }, { 7, 12 }, { 12, 17 }, { 17, 20 }, { 20, 23 },
            { 0, 9 }, { 9, 22 },
            { 2, 15 }, { 15, 24 },
            { 3, 10 }, { 10, 19 },
            { 5, 14 }, { 14, 21 },
            { 0, 3 }, { 3, 6 }, { 6, 12 },
            { 2, 5 }, { 5, 8 }, { 8, 12 },
            { 22, 19 }, { 19, 16 }, { 16, 12 },
            { 24, 21 }, { 21, 18 }, { 18, 12 },
            { 6, 11 }, { 8, 13 },
            { 16, 11 }, { 18, 13 },
            { 9, 3 }, { 3, 1 }, { 1, 5 }, { 5, 15 },
            { 19, 20 }, { 20, 21 }, { 21, 24 },
        };

        private static readonly Dictionary<int, QometNode> NodesByPosition = new();
        private static readonly Dictionary<int, List<Vector2Int>> NeighborsByPosition = new();
        private static readonly Dictionary<string, QometNode> NodesById = new();
        private static readonly List<QometEdge> EdgeList = new();

        static QometGraph()
        {
            foreach (var node in Nodes)
            {
                NodesByPosition[PositionKey(node.Row, node.Col)] = node;
                NeighborsByPosition[PositionKey(node.Row, node.Col)] = new List<Vector2Int>();
                NodesById[node.Id] = node;
            }

            for (int i = 0; i < EdgeNodeIndices.GetLength(0); i++)
            {
                var a = Nodes[EdgeNodeIndices[i, 0]];
                var b = Nodes[EdgeNodeIndices[i, 1]];
                AddEdge(a, b);
            }
        }

        public static IReadOnlyList<QometNode> AllNodes => Nodes;
        public static IReadOnlyList<QometEdge> AllEdges => EdgeList;

        public static bool IsValidNode(int row, int col)
        {
            return NodesByPosition.ContainsKey(PositionKey(row, col));
        }

        public static bool TryGetNode(int row, int col, out QometNode node)
        {
            return NodesByPosition.TryGetValue(PositionKey(row, col), out node);
        }

        public static bool TryGetNodeById(string id, out QometNode node)
        {
            if (string.IsNullOrEmpty(id))
            {
                node = default;
                return false;
            }

            return NodesById.TryGetValue(id, out node);
        }

        public static Vector2 GetPosition(int row, int col)
        {
            return TryGetNode(row, col, out var node) ? node.Position : new Vector2(col - 3f, 3f - row);
        }

        public static Vector2 GetVisualPosition(int row, int col)
        {
            return TryGetNode(row, col, out var node) ? node.VisualPosition : new Vector2(col - 3f, 3f - row);
        }

        public static bool AreConnected(int rowA, int colA, int rowB, int colB)
        {
            if (!NeighborsByPosition.TryGetValue(PositionKey(rowA, colA), out var neighbors))
            {
                return false;
            }

            var target = new Vector2Int(rowB, colB);
            return neighbors.Contains(target);
        }

        public static bool TryGetNextInDirection(Vector2Int from, Vector2Int to, out Vector2Int next)
        {
            next = default;
            if (!TryGetNode(from.x, from.y, out var fromNode) || !TryGetNode(to.x, to.y, out var toNode))
            {
                return false;
            }

            if (!NeighborsByPosition.TryGetValue(PositionKey(to.x, to.y), out var neighbors))
            {
                return false;
            }

            Vector2 move = toNode.Position - fromNode.Position;
            if (move.sqrMagnitude < 0.001f)
            {
                return false;
            }

            Vector2 direction = move.normalized;
            float bestDot = 0.985f;
            foreach (var candidate in neighbors)
            {
                if (candidate == from)
                {
                    continue;
                }

                Vector2 candidateVector = GetPosition(candidate.x, candidate.y) - toNode.Position;
                if (candidateVector.sqrMagnitude < 0.001f)
                {
                    continue;
                }

                float dot = Vector2.Dot(direction, candidateVector.normalized);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    next = candidate;
                }
            }

            return bestDot > 0.985f;
        }

        private static void AddEdge(QometNode a, QometNode b)
        {
            var aPosition = new Vector2Int(a.Row, a.Col);
            var bPosition = new Vector2Int(b.Row, b.Col);
            if (!NeighborsByPosition[PositionKey(a.Row, a.Col)].Contains(bPosition))
            {
                NeighborsByPosition[PositionKey(a.Row, a.Col)].Add(bPosition);
            }

            if (!NeighborsByPosition[PositionKey(b.Row, b.Col)].Contains(aPosition))
            {
                NeighborsByPosition[PositionKey(b.Row, b.Col)].Add(aPosition);
            }

            var edge = new QometEdge(aPosition, bPosition);
            if (!ContainsEdge(edge))
            {
                EdgeList.Add(edge);
            }
        }

        private static bool ContainsEdge(QometEdge edge)
        {
            foreach (var existing in EdgeList)
            {
                bool same = existing.A == edge.A && existing.B == edge.B;
                bool reversed = existing.A == edge.B && existing.B == edge.A;
                if (same || reversed)
                {
                    return true;
                }
            }

            return false;
        }

        private static int PositionKey(int row, int col)
        {
            return row * 16 + col;
        }
    }
}
