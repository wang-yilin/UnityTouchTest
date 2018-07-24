using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class CreateCharMenu : MonoBehaviour {

    private string fileName = "characterData.json";
    public GameObject GameManager;
    public GameObject ButtonPrefab;
    public GameObject CanvasObject;
    public GameObject ScrollBar;
    public GameObject ModeText;



    int w = Screen.width;
    int h = Screen.height;

    CharData charData;
    Character[] characters;

    public bool active = false;

	// Use this for initialization
	void Start () {

        loadData();
        characters = charData.characters;

        // Get button dimensions
        int buttonWidth = (w - 15);
        int buttonHeight = (h) / 5;

        // Set scroll bar height and initial position
        ScrollBar.GetComponent<RectTransform>().sizeDelta = new Vector2(w, characters.Length * buttonHeight);
        ScrollBar.GetComponent<RectTransform>().anchoredPosition3D = new Vector3(0, -(characters.Length * buttonHeight) / 2, 0);







        for (int i = 0; i < characters.Length; i++)
        {
            int id = i + 1;
            string name = characters[i].name;

            // Ensure characters in sequential  order by id
            if (characters[i].id != id)
            {
                Debug.LogError("JSON formatted incorrectly. " +
                               id + "th member should have ID " + id +
                               ", instead has ID " + characters[i].id);
            }

            // Create button for character
            GameObject newButton = Instantiate(ButtonPrefab);
            newButton.transform.SetParent(ScrollBar.transform);

            // Set button text
            GameObject newButtonText = newButton.transform.GetChild(0).gameObject;
            newButtonText.GetComponent<Text>().text = characters[i].display_name;

            // Name objects
            newButton.name = characters[i].name + " button";
            newButtonText.name = characters[i].name + " text";

            // Deal with character button position/size
            newButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1);
            newButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
            newButton.GetComponent<RectTransform>().sizeDelta = new Vector2(buttonWidth,buttonHeight);
            newButton.GetComponent<RectTransform>().localScale = new Vector3(1, 1, 1);
            newButton.GetComponent<RectTransform>().anchoredPosition3D = new Vector3(0,-(buttonHeight/2) - (i*buttonHeight), 0);


            // Create event listener for character selection
            newButton.GetComponent<Button>().onClick.AddListener( delegate { 
                changeCharacter(name); 
            });

        }
    }
	
    void loadData () {

        TextAsset txtAsset = Resources.Load("characterData") as TextAsset;

        if (txtAsset == null) {
            Debug.LogError("Character data file not found.");
        }

        string dataAsJson = txtAsset.text;

        charData = JsonUtility.FromJson<CharData>(dataAsJson);
    }

    void changeCharacter (string name) {
        if (active)
        {
            SendMessageUpwards("closeMenu");
            GameManager.SendMessage("SetMode", name);
            active = false;
        }
    }

    void activateCharMenu() {
        active = true;
    }

	// Update is called once per frame
	void Update () {
		
	}



}
