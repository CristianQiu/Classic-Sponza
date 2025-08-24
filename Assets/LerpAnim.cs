using UnityEngine;

public class LerpAnim : MonoBehaviour
{
	public Transform a;
	public Transform b;
	public bool reverse;
	public float speed = 1.0f;
	private float t = 0.0f;

	private float dir = 1.0f;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	private void Start()
	{
	}

	// Update is called once per frame
	private void Update()
	{
		t += Time.deltaTime * speed * dir;
		float smoothStep = Mathf.Sin(t * Mathf.PI * 0.5f);

		if (reverse)
			transform.position = Vector3.Lerp(b.position, a.position, smoothStep);
		else
			transform.position = Vector3.Lerp(a.position, b.position, smoothStep);

		if (t >= 1.0f || t <= 0.0f)
		{
			dir *= -1.0f;
			t = Mathf.Clamp01(t);
		}
	}
}