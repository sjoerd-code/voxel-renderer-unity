using System;
using System.Collections.Generic;
using UnityEngine;

public class VoxelRendererTexture : MonoBehaviour
{
    private Camera mainCamera;
    private ComputeBuffer mouseVoxelCoord;
    private Vector3 editPos;
    private RenderTexture target;
    private RenderTexture depthBuffer;
    private Texture3D data;
    public int dataSize = 64;
    public ComputeShader shader;
    public Light directionalLight;
    public Texture hdri;
    public int renderDistance = 1000;
    public int rayBounces = 2;

    private void OnEnable()
    {
        // init cam
        mainCamera = GetComponent<Camera>();

        // init mouse world space buffer
        mouseVoxelCoord = new ComputeBuffer(3, sizeof(float) * 3);
        float[] arr = new float[3];
        mouseVoxelCoord.SetData(arr);
        
        // generate voxel data
        data = GenChunkData();
        data.Apply();
    }

    private void OnDisable()
    {
        mouseVoxelCoord.Release();
    }

    private void Update()
    {
        if (Input.GetMouseButton(0)) SculptVoxelData();
    }
    
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // init buffers and variables
        target = new RenderTexture(Screen.width, Screen.height, 0,
        RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        target.enableRandomWrite = true;
        target.Create();
        depthBuffer = new RenderTexture(Screen.width, Screen.height, 0,
        RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        depthBuffer.enableRandomWrite = true;
        depthBuffer.Create();

        // pass buffers and variables
        shader.SetVector("_DirectionalLight", -directionalLight.transform.forward);
        shader.SetTexture(0, "_SkyboxTexture", hdri);
        shader.SetMatrix("_CameraToWorld", mainCamera.cameraToWorldMatrix);
        shader.SetMatrix("_CameraInverseProjection", mainCamera.projectionMatrix.inverse);
        shader.SetFloat("_rayBounces", rayBounces);
        shader.SetFloat("_renderdistance", renderDistance);
        shader.SetVector("_mousePos", Input.mousePosition);
        shader.SetBuffer(0, "_MouseVoxelCoord", mouseVoxelCoord);
        shader.SetVector("CamPos", mainCamera.transform.position);
        shader.SetFloat("_ChunkSize", dataSize);
        shader.SetTexture(0, "Result", target);
        shader.SetTexture(0, "DepthBuffer", depthBuffer);
        shader.SetTexture(0, "_VoxelData", data);

        // dispatch compute shader
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        shader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        Graphics.Blit(target, destination);

        // retrieve buffers
        float[] CoordArray = new float[3];
        mouseVoxelCoord.GetData(CoordArray);
        editPos = new Vector3(CoordArray[0], CoordArray[1], CoordArray[2]);

        // release buffers
        target.Release();
        depthBuffer.Release();
    }

    Texture3D GenChunkData()
    {
        Texture3D tex = new Texture3D(dataSize, dataSize, dataSize, TextureFormat.RHalf, 0);
        tex.filterMode = FilterMode.Point;

        for (int x = 0; x < dataSize; x++)
        {
            for (int y = 0; y < dataSize; y++)
            {
                for (int z = 0; z < dataSize; z++)
                {
                    if (Vector3.Distance(new Vector3(x,y,z), Vector3.one * dataSize / 2) < dataSize / 2)
                    {
                        tex.SetPixel(x, y, z, new Color(1, 0, 0));
                    }
                }
            }
        }
        return tex;
    }

    void SculptVoxelData()
    {
        List<Vector3Int> voxelsToRemove = new List<Vector3Int>();

        int radius = 20;
        int t = radius / 2;
        for (int x = 0; x < radius; x++)
        {
            for (int y = 0; y < radius; y++)
            {
                for (int z = 0; z < radius; z++)
                {
                    int a = x - t;
                    int b = y - t;
                    int c = z - t;
                    Vector3Int editpos = new Vector3Int((int)editPos.x, (int)editPos.y, (int)editPos.z);
                    Vector3Int voxelPos = new Vector3Int(editpos.x + a, editpos.y + b, editpos.z + c);

                    if (Vector3.Distance(editpos, voxelPos) < radius / 2)
                    {
                        if (data.GetPixel(voxelPos.x, voxelPos.y, voxelPos.z).r > 0)
                        {
                            voxelsToRemove.Add(new Vector3Int(voxelPos.x, voxelPos.y, voxelPos.z));
                        }
                    }
                }
            }
        }

        foreach (Vector3Int pos in voxelsToRemove) data.SetPixel(pos.x, pos.y, pos.z, new Color(0, 0, 0));
        data.Apply();
    }
}