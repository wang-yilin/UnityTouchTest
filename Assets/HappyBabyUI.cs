using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HappyBabyUI : MonoBehaviour {
    public GameObject OpenMenuButton;
    public GameObject ModeMenu;

    private RectTransform openMenuTrans;
    private RectTransform modeMenuTrans;

    int w = Screen.width;
    int h = Screen.height;

    bool menuOpening = false;
    bool menuClosing = false;

    public float tol = 0.01f;

	private void Start()
	{
        openMenuTrans = OpenMenuButton.GetComponent<RectTransform>();
        modeMenuTrans = ModeMenu.GetComponent<RectTransform>();
        modeMenuTrans.anchoredPosition3D = new Vector3(-w - 5, 0, 0f);
	}


	private void Update()
	{

        if (menuOpening){

            ModeMenu.GetComponent<CreateCharMenu>().active = true;

            openMenuTrans.anchoredPosition3D =
                             Vector3.Lerp(openMenuTrans.anchoredPosition3D,
                                          new Vector3(150f, -66f, 0f),
                                           .1f);
            modeMenuTrans.anchoredPosition3D =
                             Vector3.Lerp(modeMenuTrans.anchoredPosition3D,
                                           new Vector3(0, 0, 0),
                                           .1f);
            if ((modeMenuTrans.anchoredPosition3D - new Vector3(0, 0, 0)).magnitude < tol)
            {
                menuOpening = false;
            }
        }
        else if (menuClosing) {
            openMenuTrans.anchoredPosition3D =
                             Vector3.Lerp(openMenuTrans.anchoredPosition3D,
                                           new Vector3(-103f, -66f, 0f),
                                           .1f);
            modeMenuTrans.anchoredPosition3D =
                             Vector3.Lerp(modeMenuTrans.anchoredPosition3D,
                                          new Vector3(-w-5, 0, 0),
                                           .1f);
            if ((modeMenuTrans.anchoredPosition3D - new Vector3(-w - 5, 0, 0)).magnitude < tol) {
                menuClosing = false;
            }
        }

	}

    void openMenu()
    {
        menuClosing = false;
        menuOpening = true;
    }

    void closeMenu()
    {
        menuOpening = false;
        menuClosing = true;
    }
}
