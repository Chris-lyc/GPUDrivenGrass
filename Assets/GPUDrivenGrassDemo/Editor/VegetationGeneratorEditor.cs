using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Random = UnityEngine.Random;

using GPUDrivenGrassDemo.Runtime;

namespace GPUDrivenGrassDemo.Editor
{
    public class VegetationGeneratorEditor: EditorWindow
    {
        public static VegetationGeneratorEditor window;
        public GameObject prefab;
        public int instanceCount;
        
        DefaultAsset vegetationDatabaseDirAsset = null;
        string vegetationDatabaseDir = null;
        string rawVegetationDatabaseFilename = "VegetationDatabase";
        string rawVegetationDatabaseFileExtension = ".asset";
        
        private Terrain[] terrains;
        private Dictionary<int, GameObject> modelPrototypeDic = new Dictionary<int, GameObject>();
        
        [MenuItem("GPUDrivenGrass/VegetationGenerator")]
        public static void OpenWindow()
        {
            window = GetWindow<VegetationGeneratorEditor>("VegetationGenerator");
            window.minSize = new Vector2(400, 400);
        }

        public void OnGUI()
        {
            prefab = EditorGUILayout.ObjectField
                ("Vegetation Prefab: ", prefab, typeof(GameObject), true) as GameObject;
            instanceCount = EditorGUILayout.IntField("Instance count of a chunk: ", instanceCount);

            vegetationDatabaseDirAsset = EditorGUILayout.ObjectField("Database path: ", vegetationDatabaseDirAsset,
                typeof(DefaultAsset), allowSceneObjects: false) as DefaultAsset;
            if (vegetationDatabaseDirAsset != null)
                vegetationDatabaseDir = AssetDatabase.GetAssetPath(vegetationDatabaseDirAsset);
            else vegetationDatabaseDir = null;
            rawVegetationDatabaseFilename = EditorGUILayout.TextField("Database name:", rawVegetationDatabaseFilename);
            
            if (GUILayout.Button("Generate vegetation", GUILayout.Height(30)))
            {
                GenerateVegetation();
            }
        }

        public void GenerateVegetation()
        {

            if (string.IsNullOrEmpty(vegetationDatabaseDir) ||
                string.IsNullOrEmpty(rawVegetationDatabaseFilename)) return;
            System.IO.Directory.CreateDirectory(vegetationDatabaseDir);

            string fullFilePath = System.IO.Path.Combine(vegetationDatabaseDir,
                rawVegetationDatabaseFilename + rawVegetationDatabaseFileExtension);
            if (File.Exists(fullFilePath))
            {
                File.Delete(fullFilePath);
            }
            
            VegetationDataBase database = ScriptableObject.CreateInstance<VegetationDataBase>();
            AssetDatabase.CreateAsset(database, fullFilePath);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            terrains = Terrain.activeTerrains;
            database.terrainCount = terrains.Length;
            
            database.instanceCount = instanceCount;
            
            List<VegetationInstanceData> instanceList = new List<VegetationInstanceData>();

            int index = 0;
            foreach (var terrain in terrains)
            {
                // Get terrain data
                TerrainData terrainData = terrain.terrainData;
            
                for (int i = 0; i < instanceCount; i++)
                {
                    var x = Random.Range(0, terrain.terrainData.size.x) + terrain.transform.position.x;
                    var z = Random.Range(0, terrain.terrainData.size.z) + terrain.transform.position.z;
                    var pos = new Vector3(x, 0, z);
                    
                    var vd = GetVegetationInstanceData(pos, new Vector2(1f, 1f), prefab, index, terrain);
                    index++;
                    
                    instanceList.Add(vd);
                }
            }
            
            // Assign generated instance list to scriptable object
            database.vegetationInstanceDataList = instanceList;

            List<ModelPrototype> modelPrototypes = new List<ModelPrototype>();
            foreach (var item in modelPrototypeDic)
            {
                modelPrototypes.Add(new ModelPrototype(item.Key, item.Value));
            }
            database.modelPrototypeList = modelPrototypes;
            
            modelPrototypeDic.Clear();
            
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            
            Debug.LogFormat("Vegetation generation finished!");
        }
        
        private VegetationInstanceData GetVegetationInstanceData(Vector3 pos, Vector2 scaleRange, GameObject prefab,
            int NextInstanceID, Terrain terrain)
        {
            GetRandomRotationScaleValue(pos, scaleRange, out Vector3 p, out Vector3 _r, out Vector3 s, terrain);
            var r = Quaternion.Euler(_r);
            var mat = Matrix4x4.TRS(p, r, s);

            GameObject go = null;
            int prefabID = prefab.GetInstanceID();
            if (!modelPrototypeDic.TryGetValue(prefabID, out go))
            {
                go = prefab;
                modelPrototypeDic[prefabID] = go;
            }
            // go.transform.position = p;
            // go.transform.rotation = r;
            // go.transform.localScale = s;
            var bounds = GPUDrivenGrassDemo.Runtime.Tool.GetBounds(go);

            var vd = new VegetationInstanceData();
            vd.matrix = mat;
            vd.center = bounds.center;
            vd.extents = bounds.extents;
            vd.InstanceID = NextInstanceID;
            vd.ModelPrototypeID = prefabID;
            return vd;
        }

        private void GetRandomRotationScaleValue(Vector3 pos, Vector2 scaleRange, out Vector3 p, out Vector3 r,
            out Vector3 s, Terrain terrain)
        {
            p = GetPositionInTerrain(pos, terrain);
            r = new Vector3(0, Random.Range(0f, 360f), 0);
            s = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);
        }

        private Vector3 GetPositionInTerrain(Vector3 pos, Terrain terrain)
        {
            var _pos = pos;
            if (pos.y < terrain.terrainData.size.y)
                pos.y += terrain.terrainData.size.y;
            Ray ray = new Ray(pos, -Vector3.up);
            Vector3 targetPos = Vector3.zero;
            if (Physics.Raycast(ray, out RaycastHit result)) targetPos = result.point;
            else targetPos = _pos;
            return targetPos;
        }
    }    
}

