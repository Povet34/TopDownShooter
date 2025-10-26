/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using CodeMonkey.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldOfView : MonoBehaviour {

    [SerializeField] Transform playerTR;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] float fov;
    [SerializeField] float viewDistance;
    [SerializeField] float startingAngle;
    [SerializeField] int rayCount = 50;

    private Mesh mesh;
    private Vector3 origin;
    

    private void Start() {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        
    }

    private void LateUpdate() {

        SetAimDirection(playerTR.forward);
        SetOrigin(playerTR.position);

        float angle = startingAngle;
        float angleIncrease = fov / rayCount;

        Vector3[] vertices = new Vector3[rayCount + 1 + 1];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[rayCount * 3];

        vertices[0] = origin;

        int vertexIndex = 1;
        int triangleIndex = 0;
        for (int i = 0; i <= rayCount; i++) {
            Vector3 vertex = Vector3.zero;
            RaycastHit raycastHit;

            if(Physics.Raycast(origin, UtilsClass.GetVectorFromAngle(angle), out raycastHit, viewDistance, layerMask))
            {
                if (raycastHit.collider)
                    vertex = raycastHit.point;
                else
                    vertex = origin + UtilsClass.GetVectorFromAngle(angle) * viewDistance;
            }
            else
                vertex = origin + UtilsClass.GetVectorFromAngle(angle) * viewDistance;
            vertices[vertexIndex] = vertex;


            if (i > 0)
            {
                triangles[triangleIndex + 0] = 0;
                triangles[triangleIndex + 1] = vertexIndex - 1;
                triangles[triangleIndex + 2] = vertexIndex;

                triangleIndex += 3;
            }

            vertexIndex++;
            angle -= angleIncrease;
        }


        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(origin, Vector3.one * 1000f);
    }

    public void SetOrigin(Vector3 origin) {
        this.origin = origin;
    }

    public void SetAimDirection(Vector3 aimDirection) {
        startingAngle = UtilsClass.GetAngleFromVectorFloat(aimDirection) + fov / 2f;
    }

    public void SetFoV(float fov) {
        this.fov = fov;
    }

    public void SetViewDistance(float viewDistance) {
        this.viewDistance = viewDistance;
    }

}
