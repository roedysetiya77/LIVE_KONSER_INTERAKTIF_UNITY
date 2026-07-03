using UnityEngine;

public class MenuController : MonoBehaviour
{
    private Animator animator;
    private bool isHidden = false; // Status apakah menu sedang sembunyi

    void Start()
    {
        // Mengambil komponen Animator dari Panel
        animator = GetComponent<Animator>();
    }

    public void ToggleMenu()
    {
        if (isHidden)
        {
            // Jika sedang sembunyi, putar animasi muncul
            animator.Play("Menu_Muncul");
            isHidden = false;
        }
        else
        {
            // Jika sedang muncul, putar animasi sembunyi
            animator.Play("Menu_Sembunyi");
            isHidden = true;
        }
    }
}