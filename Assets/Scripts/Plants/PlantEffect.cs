using UnityEngine;
[RequireComponent(typeof(Plant))]
public class PlantEffect : MonoBehaviour

{
    private Plant plant;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        plant = GetComponent<Plant>();
    }

    // Update is called once per frame
    void Update()
    {
        if (plant != null)
        {
            if (plant.stage == Plant.GrowthStage.Mature)
            {
                gameObject.GetComponent<Collider2D>().isTrigger = false;
            }
            // Aquí puedes agregar efectos de planta basados en el estado de la planta
        }
    }
    
}
