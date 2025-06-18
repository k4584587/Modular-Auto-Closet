using UnityEngine;

public class ClosetToggleExample : MonoBehaviour
{
    public GameObject[] clothes; // clothing items to toggle with numeric keys
    private int activeIndex = 0;
    void Start()
    {
        ActivateClothes(activeIndex);
    }

    void Update()
    {
        for (int i = 0; i < clothes.Length; i++)
        {
            if (Input.GetKeyDown((i + 1).ToString()))
            {
                ActivateClothes(i);
            }
        }
    }

    private void ActivateClothes(int index)
    {
        for (int i = 0; i < clothes.Length; i++)
        {
            if (clothes[i] != null)
            {
                clothes[i].SetActive(i == index);
            }
        }
    }
}
