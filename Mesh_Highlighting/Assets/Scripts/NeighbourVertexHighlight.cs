/*
    This class creates a connection graph for neighbour vertices from the parent mesh and
    subsequently generates a line mesh that highlights neighbour vertices.
 */

using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NeighbourVertexHighlight : MonoBehaviour
{
    private Mesh highlightMesh;
    private Mesh parentMesh; // Refers to the mesh we are getting the indices to highlight

    private List<int>[] neighbourGraph; // Pre-computed neighbour graph

    private Dictionary<Vector3, VertexNeighbour> indexNeighbours; // Contains the current indices that we want to highlight

    List<Vector3> vertices = new List<Vector3>();
    List<int> indices = new List<int>();
    Dictionary<int, int> indexRemap = new Dictionary<int, int>();

    private bool finishedProcessingNeighbourGraph = false;

    void Start()
    {
        indexNeighbours = new Dictionary<Vector3, VertexNeighbour>();

        if (transform.parent == null)
            Debug.LogError("NeighbourHighlight needs to be attached to a child of the mesh one wants to highlight");

        parentMesh = transform.parent.GetComponent<MeshFilter>().mesh;

        if (parentMesh == null)
            Debug.LogError("Parent GameObject doesn't have a mesh");

        Material meshMat = new Material(Shader.Find("Unlit/Color"));
        meshMat.color = Color.red;

        gameObject.AddComponent<MeshRenderer>().material = meshMat;
        gameObject.AddComponent<MeshFilter>();

        /* Neighbour Graph generation */
        ProcessNeighbourGraph();
    }

    private void ThreadedNeighbourGraphProcess(Vector3[] parentVerts, int[] parentTriangs)
    {
        finishedProcessingNeighbourGraph = false;
        Debug.Log("Generating neighbour hierarchy in other thread...");

        neighbourGraph = GenerateNeighbourGraph(parentVerts, parentTriangs);

        Debug.Log("Finished generating hierarchy!");
        finishedProcessingNeighbourGraph = true;
    }

    private void ProcessNeighbourGraph()
    {
        finishedProcessingNeighbourGraph = false;

        Vector3[] parentVerts = parentMesh.vertices;
        int[] parentTriangs = parentMesh.triangles;

        Thread graphWorker = new Thread(new ThreadStart(() => ThreadedNeighbourGraphProcess(parentVerts, parentTriangs)));
        graphWorker.Start();
    }

    public void AddIndex(int i)
    {
        if (!finishedProcessingNeighbourGraph)
        {
            Debug.LogError("Can't add index as neighbour graph is being processed!");
            return;
        }

        if (!indexNeighbours.ContainsKey(parentMesh.vertices[i]))
        {
            indexNeighbours.Add(parentMesh.vertices[i], new VertexNeighbour(i, neighbourGraph[i]));
            AddVertexToMesh(parentMesh.vertices[i], i);
        }
    }

    public void RemoveIndex(int i)
    {
        if (!finishedProcessingNeighbourGraph)
        {
            Debug.LogError("Can't remove index as neighbour graph is being processed!");
            return;
        }

        if (indexNeighbours.ContainsKey(parentMesh.vertices[i]))
        {
            indexNeighbours.Remove(parentMesh.vertices[i]);
            RemoveVertexFromMesh(parentMesh.vertices[i], i);
        }
    }

    private void AddVertexToMesh(Vector3 vert, int index)
    {
        highlightMesh = new Mesh();

        List<int> neighboursOfIndex = neighbourGraph[index];

        if (!indexRemap.ContainsKey(index))
        {
            vertices.Add(vert);
            indexRemap.Add(index, vertices.Count - 1);
        }

        for (int i = 0; i < neighboursOfIndex.Count; i++)
        {
            indices.Add(indexRemap[index]);

            if (indexRemap.ContainsKey(neighboursOfIndex[i]))
            {
                indices.Add(indexRemap[neighboursOfIndex[i]]);
            }
            else
            {
                vertices.Add(parentMesh.vertices[neighboursOfIndex[i]]);
                indexRemap.Add(neighboursOfIndex[i], vertices.Count - 1);
                indices.Add(indexRemap[neighboursOfIndex[i]]);
            }
        }

        highlightMesh.SetVertices(vertices);
        highlightMesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);

        GetComponent<MeshFilter>().sharedMesh = highlightMesh;
    }

    private void RemoveVertexFromMesh(Vector3 vert, int index)
    {
        highlightMesh = new Mesh();

        if (indexRemap.ContainsKey(index))
        {
            for (int i = 0; i < indices.Count - 1; i++)
            {
                if (indices[i] == indexRemap[index])
                {
                    indices[i] = -1;
                    indices[i + 1] = -1;
                }
            }

            indices.RemoveAll(x => x == -1);
        }

        highlightMesh.SetVertices(vertices);
        highlightMesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);

        GetComponent<MeshFilter>().sharedMesh = highlightMesh;
    }

    private List<int>[] GenerateNeighbourGraph(Vector3[] _vertices, int[] _triangles)
    {
        List<int>[] neighbour = new List<int>[_vertices.Length];

        Dictionary<Vector3, int> currentConnections = new Dictionary<Vector3, int>();

        for (int j = 0; j < _triangles.Length; j += 3)
        {
            int j1 = _triangles[j + 0];
            int j2 = _triangles[j + 1];
            int j3 = _triangles[j + 2];

            if (neighbour[j1] == null) neighbour[j1] = new List<int>();
            if (neighbour[j2] == null) neighbour[j2] = new List<int>();
            if (neighbour[j3] == null) neighbour[j3] = new List<int>();

            if (!currentConnections.ContainsKey(_vertices[j1]))
                currentConnections.Add(_vertices[j1], j1);

            if (!currentConnections.ContainsKey(_vertices[j2]))
                currentConnections.Add(_vertices[j2], j2);

            if (!currentConnections.ContainsKey(_vertices[j3]))
                currentConnections.Add(_vertices[j3], j3);

            neighbour[currentConnections[_vertices[j1]]].Add(j2);
            neighbour[currentConnections[_vertices[j1]]].Add(j3);

            neighbour[currentConnections[_vertices[j2]]].Add(j1);
            neighbour[currentConnections[_vertices[j2]]].Add(j3);

            neighbour[currentConnections[_vertices[j3]]].Add(j1);
            neighbour[currentConnections[_vertices[j3]]].Add(j2);
        }

        return neighbour;
    }

    // Contains index of a vertex and its neighbour indexes
    private class VertexNeighbour
    {
        private int index;
        public int Index { get { return index; } }

        private List<int> neighbourIndexes;
        public List<int> NeighbourIndexes { get { return neighbourIndexes; } }

        public VertexNeighbour(int i, List<int> indexes)
        {
            index = i;
            neighbourIndexes = indexes;
        }
    }
}