using UnityEngine;
using UnityEngine.UI;

/// <summary>画像ファイルに依存しない、筆むらのある円相の装飾。</summary>
public sealed class InkRingGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;float radius=Mathf.Min(r.width,r.height)*.45f;
        for(int i=0;i<=160;i++)
        {
            float t=i/160f,a=t*Mathf.PI*2;float wobble=1+.017f*Mathf.Sin(11*a)+.01f*Mathf.Cos(23*a);
            float width=radius*(.1f+.055f*Mathf.Sin(a+.7f)+.02f*Mathf.Cos(17*a));
            var direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));float fade=Mathf.Lerp(.2f,1,Mathf.SmoothStep(0,.12f,t));
            var vertex=UIVertex.simpleVert;vertex.color=color*new Color(1,1,1,fade);vertex.position=r.center+direction*(radius*wobble);vh.AddVert(vertex);
            vertex.position=r.center+direction*(radius*wobble-width);vh.AddVert(vertex);
            if(i>0){int n=i*2;vh.AddTriangle(n-2,n,n-1);vh.AddTriangle(n,n+1,n-1);}
        }
    }
}
