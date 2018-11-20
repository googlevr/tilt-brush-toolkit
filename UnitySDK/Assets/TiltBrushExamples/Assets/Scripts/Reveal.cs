using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Reveal : MonoBehaviour {

	public bool runOnStart = true;
	public bool hideOnStart = true;
	public float startDelay = 1f;
	public float revealDelay = 0.1f;

	private List<Renderer> rens;
	private int counter = 0;

	private void Awake() {
		initRenderers();
	}

	private void Start() {
		if (hideOnStart) showAll(false);
		if (runOnStart) runReveal();
	}

	public void runReveal() {
		StartCoroutine(runRevealCo(true));
	}

	private void initRenderers() {
		rens = new List<Renderer>();

		for (int i = 0; i < transform.childCount; i++) {
			rens.Add(transform.GetChild(i).GetComponent<Renderer>());
		}
	}

	public void showAll(bool b) {
		for (int i = 0; i < rens.Count; i++) {
			rens[i].enabled = b;
		}		
	}

	private IEnumerator runRevealCo(bool b) {
		yield return new WaitForSeconds(startDelay);

		for (int i = 0; i < rens.Count; i++) {
			rens[i].enabled = b;
			yield return new WaitForSeconds(revealDelay);
		}

	}

}
