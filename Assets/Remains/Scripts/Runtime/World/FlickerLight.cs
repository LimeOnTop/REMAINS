using UnityEngine;

namespace Remains
{
    /// <summary>Makes a torch/campfire light flicker.</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [SerializeField] private float amount = 0.25f;
        [SerializeField] private float speed = 7f;

        private Light m_Light;
        private float m_Base;
        private float m_Seed;

        private void Awake()
        {
            m_Light = GetComponent<Light>();
            m_Base = m_Light.intensity;
            m_Seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise(m_Seed, Time.time * speed);
            m_Light.intensity = m_Base * (1f - amount + noise * amount * 2f);
        }
    }
}
