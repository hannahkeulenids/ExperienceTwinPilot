using UnityEngine;
using UnityEngine.UI;

public class TabletopOptionSliderProxy : MonoBehaviour
{

    public void OnSliderValueChanged()
    {
        if (BuildManager.Instance == null)
        {
            Debug.LogError("[TabletopOptionSliderProxy] Geen BuildManager.Instance gevonden.");
            return;
        }

        float value = this.gameObject.GetComponent<Slider>().value;
        int index = Mathf.RoundToInt(value);
        BuildManager.Instance.OnTabletopOptionChanged(index);
    }
}
