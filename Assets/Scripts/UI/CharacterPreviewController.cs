using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace GameUI
{
    /// <summary>
    /// 背包 3D 角色预览 —— 专用展示模型 + Camera 渲染到 RenderTexture。
    /// 左键横拖旋转（水平 360°）。
    /// </summary>
    public class CharacterPreviewController : MonoBehaviour, IDragHandler
    {
        [Header("展示模型")]
        [SerializeField] private GameObject _displayModel;
        [SerializeField] private Animator _displayAnimator;

        [Header("绑定")]
        [SerializeField] private RawImage _displayImage;

        [Header("相机参数")]
        [SerializeField] private float _distance = 2.5f;
        [SerializeField] private float _heightOffset = 1.2f;
        [SerializeField] private float _rotationSpeed = 2f;
        [SerializeField] private Vector2Int _rtSize = new Vector2Int(512, 720);

        private Camera _previewCam;
        private RenderTexture _rt;
        private float _angle;

        private static readonly int ParamIsShowcase = Animator.StringToHash("IsShowcase");

        private void Awake()
        {
            if (_displayImage == null)
                _displayImage = GetComponent<RawImage>();

            CreateRenderTexture();
            CreateCamera();
        }

        private void CreateRenderTexture()
        {
            _rt = new RenderTexture(_rtSize.x, _rtSize.y, 24);
            _rt.name = "CharacterPreviewRT";
            if (_displayImage != null)
                _displayImage.texture = _rt;
        }

        private void CreateCamera()
        {
            var camGo = new GameObject("PreviewCamera");
            camGo.SetActive(false);

            _previewCam = camGo.AddComponent<Camera>();
            _previewCam.cullingMask = 1 << LayerMask.NameToLayer("PlayerPreview");
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.backgroundColor = new Color(0, 0, 0, 0);
            _previewCam.targetTexture = _rt;
            _previewCam.fieldOfView = 30f;
            _previewCam.nearClipPlane = 0.1f;
            _previewCam.farClipPlane = 50f;
            _previewCam.allowHDR = false;
            _previewCam.allowMSAA = false;
        }

        public void Enable()
        {
            _angle = 180f;

            if (_displayModel != null)
                _displayModel.SetActive(true);

            UpdateCameraPosition();
            _previewCam.gameObject.SetActive(true);

            if (_displayAnimator != null)
                _displayAnimator.SetBool(ParamIsShowcase, true);

            Debug.Log($"[Preview] Enabled — model:{(_displayModel != null ? _displayModel.name : "NULL")} cam:{_previewCam?.name}");
        }

        public void Disable()
        {
            _previewCam.gameObject.SetActive(false);

            if (_displayModel != null)
                _displayModel.SetActive(false);

            if (_displayAnimator != null)
                _displayAnimator.SetBool(ParamIsShowcase, false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_displayModel == null) return;
            _angle += eventData.delta.x * _rotationSpeed;
            UpdateCameraPosition();
        }

        private void UpdateCameraPosition()
        {
            if (_displayModel == null) return;
            var target = _displayModel.transform;
            float rad = _angle * Mathf.Deg2Rad;
            var pos = target.position + new Vector3(Mathf.Sin(rad) * _distance, _heightOffset, Mathf.Cos(rad) * _distance);
            _previewCam.transform.position = pos;
            _previewCam.transform.LookAt(target.position + Vector3.up * _heightOffset);
        }

        private void OnDestroy()
        {
            if (_rt != null)
            {
                _rt.Release();
                _rt = null;
            }
        }
    }
}
