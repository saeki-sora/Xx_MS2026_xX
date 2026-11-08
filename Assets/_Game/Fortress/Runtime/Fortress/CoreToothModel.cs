using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアの見た目を3Dモデル（歯）にしたときの、モデルの入れ物（コアの子）。全ステージ共通（Game シーンのコア）。
    /// ステージ背景スタジオの「コア」ページが作り、ここに置き方（大きさ・向き）を覚えておく。
    /// この入れ物の下のモデルを <see cref="CoreToothLook"/> が HP に合わせて汚す・光らせる・ひびを入れる。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MS2026/要塞/コアの3Dモデル（歯）")]
    public sealed class CoreToothModel : MonoBehaviour
    {
        /// <summary>入れ物の GameObject の名前。</summary>
        public const string ObjectName = "[Core] Model";

        [Tooltip("元のモデル（FBX・Prefab）。置き直すときに使う。")]
        public GameObject sourceModel;

        [Range(0.3f, 10f), Tooltip("床の上で見たときの幅（メートル）。まっすぐ立てたときの幅で決めるので、回したり傾けたりしても大きさは変わらない。")]
        public float widthMeters = 2.5f;

        [Range(-180f, 180f), Tooltip("床の上での向き（度）。")]
        public float yawDegrees;

        [Tooltip("Maya で作ったモデル（上が Y）を、ゲームの床（上が画面の手前 −Z）に合わせて起こす。")]
        public bool mayaUp = true;

        [Range(-2f, 2f), Tooltip("床にめり込ませる深さ（メートル）。0=底がちょうど床の上。マイナスで浮く。")]
        public float sinkMeters;

        [Range(-3f, 3f), Tooltip("左右のずれ（メートル。＋で右）。見た目だけ動き、敵が当たる場所（コアの位置）は変わらない。")]
        public float offsetX;

        [Range(-3f, 3f), Tooltip("上下のずれ（メートル。＋で画面の上）。見た目だけ動き、敵が当たる場所（コアの位置）は変わらない。")]
        public float offsetY;

        [Range(-90f, 90f), Tooltip("前後の傾き（度。＋で歯の上が画面の上へ倒れる）。傾けても底は床の上に置き直す。")]
        public float tiltForward;

        [Range(-90f, 90f), Tooltip("左右の傾き（度。＋で歯の上が右へ倒れる）。傾けても底は床の上に置き直す。")]
        public float tiltSide;

        [Tooltip("ON: どの席から見ても、1P の席から見たときと同じ向きに見えるように、カメラの画面の回転（席替えの途中も）に合わせて歯を回す。" +
                 "見た目だけで、各PCが自分のカメラに合わせて回す（通信しない）。OFF: 床に置いたまま（席によって見える面が変わる）。" +
                 "置き方の「上下・左右・前後」は 1P の画面での向き。")]
        public bool sameViewForEveryone = true;

        /// <summary>入れ物の下の、見た目の部品（Mesh / SkinnedMesh）。</summary>
        public void CollectRenderers(System.Collections.Generic.List<Renderer> results)
        {
            results.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    results.Add(renderer);
                }
            }
        }

        /// <summary>今、歯が見えているか（入れ物が有効で、見た目の部品がある）。</summary>
        public bool IsShowing => isActiveAndEnabled && GetComponentInChildren<MeshRenderer>() != null;
    }
}
