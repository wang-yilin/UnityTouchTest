using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {

    public Text ModeText;
    public GameObject bg;
    Material bgMat;
    public GameObject iphone;

    /* Modes:
     * 1) Baby
     * 2) Whale
     * 3) Lemur
     * 4) Bird
     * 5) Noodle
     * 6) Rock
     * 7) Bee
     */
    private string mode;

    private float happiness;

	// Use this for initialization
	void Start () {
        happiness = 0;
	}

    void SetMode(string newName) {
        mode = newName;
        Debug.Log(mode);
        ModeText.GetComponent<Text>().text = "Current Mode: " + mode;
        // change the patters
        iphone.GetComponent<AccelTest>().ChangeCharacterPatterns(mode);
    }
	
	// Update is called once per frame
	void Update () {
        happiness = Mathf.PingPong(Time.time * 0.125f,1.0f);

       // bg.GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.HSVToRGB((235f / 360f) * happiness, 1, 1));
        bg.GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.HSVToRGB(iphone.GetComponent<AccelTest>().hue, 1, 1));
	}
}
