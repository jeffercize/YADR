using Godot;
using System;

public partial class WeatherController : Node3D
{
	DirectionalLight3D mySun;
	ProceduralSkyMaterial mySkyMaterial;
	WorldEnvironment myEnvironment;

    [Export]
    GpuParticles3D myRainMaker;
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
        myEnvironment = new WorldEnvironment();
        myRainMaker = GetNode<GpuParticles3D>("/root/TerrainGeneration/Player/RainGPUParticles");//"/root/main/LevelManager/Player/RainGPUParticles");
        myEnvironment.Environment = new Godot.Environment();
		myEnvironment.Environment.BackgroundMode = Godot.Environment.BGMode.Sky;

		mySkyMaterial = new ProceduralSkyMaterial();
		mySkyMaterial.SkyTopColor = new Color(0.2f, 0.3f, 0.5f);
        mySkyMaterial.SkyHorizonColor = new Color(0.5f, 0.6f, 0.7f);
        mySkyMaterial.GroundBottomColor = new Color(0.2f, 0.3f, 0.5f);
        mySkyMaterial.GroundHorizonColor = new Color(0.5f, 0.6f, 0.7f);
		mySkyMaterial.GroundCurve = 0.13f;

        myEnvironment.Environment.Sky = new Sky();
        myEnvironment.Environment.Sky.SkyMaterial = mySkyMaterial;
		myEnvironment.Environment.AmbientLightColor = new Color(1f, 1f, 1f);
		myEnvironment.Environment.AmbientLightSkyContribution = 0.5f;
		myEnvironment.Environment.AmbientLightEnergy = 0.2f;
		myEnvironment.Environment.TonemapMode = Godot.Environment.ToneMapper.Aces;

        myEnvironment.Environment.VolumetricFogEnabled = true;
        myEnvironment.Environment.VolumetricFogDensity = 0.02f;
        myEnvironment.Environment.VolumetricFogEmission = new Color(0.15f, 0.15f, 0.15f);

        mySun = new DirectionalLight3D();

        mySun.DirectionalShadowMaxDistance = 256.0f;
        mySun.Rotation = new Vector3(100.0f, 0.0f, 0.0f);
        mySun.LightColor = (new Color(0.5f, 0.5f, 0.5f));
        mySun.LightEnergy = 0.2f;
		mySun.ShadowEnabled = true;


        AddChild(myEnvironment);
        AddChild(mySun);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        if (Input.IsKeyPressed(Key.I))
        {
            MakeItSunny();
            RenderingServer.GlobalShaderParameterSet("windStrength", 0.0);
            myRainMaker.Emitting = false;
        }
        else if (Input.IsKeyPressed(Key.O))
        {
            MakeItCloudy();
            RenderingServer.GlobalShaderParameterSet("windStrength", 0.5);
            myRainMaker.Emitting = false;
        }
        else if (Input.IsKeyPressed(Key.P))
        {
            MakeItHeavyRain();
            RenderingServer.GlobalShaderParameterSet("windStrength", 1.0);
            myRainMaker.Emitting = true;
        }
    }

    public void MakeItHeavyRain()
    {
        myEnvironment.Environment.BackgroundMode = Godot.Environment.BGMode.Sky;

        mySkyMaterial.SkyTopColor = new Color(0.2f, 0.25f, 0.3f);
        mySkyMaterial.SkyHorizonColor = new Color(0.23f, 0.3f, 0.37f);
        mySkyMaterial.GroundBottomColor = new Color(0.2f, 0.25f, 0.3f);
        mySkyMaterial.GroundHorizonColor = new Color(0.23f, 0.3f, 0.37f);
        mySkyMaterial.GroundCurve = 0.13f;

        myEnvironment.Environment.Sky.SkyMaterial = mySkyMaterial;
        myEnvironment.Environment.AmbientLightColor = new Color(0.66f, 0.66f, 0.66f);
        myEnvironment.Environment.AmbientLightSkyContribution = 0.5f;
        myEnvironment.Environment.AmbientLightEnergy = 0.2f;
        myEnvironment.Environment.TonemapMode = Godot.Environment.ToneMapper.Aces;

        myEnvironment.Environment.VolumetricFogEnabled = true;
        myEnvironment.Environment.VolumetricFogDensity = 0.02f;
        myEnvironment.Environment.VolumetricFogEmission = new Color(0.15f, 0.15f, 0.15f);

/*        mySun.DirectionalShadowMaxDistance = 256.0f;
        mySun.RotateX(-60);
        mySun.LightColor = (new Color(0.5f, 0.5f, 0.5f));
        mySun.LightEnergy = 0.2f;
        mySun.ShadowEnabled = true;*/
        mySun.Visible = false;

        //tell particles to rain
    }

    public void MakeItCloudy()
    {
        myEnvironment.Environment.BackgroundMode = Godot.Environment.BGMode.Sky;

        mySkyMaterial.SkyTopColor = new Color(0.2f, 0.25f, 0.3f);
        mySkyMaterial.SkyHorizonColor = new Color(0.23f, 0.3f, 0.37f);
        mySkyMaterial.GroundBottomColor = new Color(0.2f, 0.25f, 0.3f);
        mySkyMaterial.GroundHorizonColor = new Color(0.23f, 0.3f, 0.37f);
        mySkyMaterial.GroundCurve = 0.13f;

        myEnvironment.Environment.Sky.SkyMaterial = mySkyMaterial;
        myEnvironment.Environment.AmbientLightColor = new Color(0.66f, 0.66f, 0.66f);
        myEnvironment.Environment.AmbientLightSkyContribution = 0.5f;
        myEnvironment.Environment.AmbientLightEnergy = 0.2f;
        myEnvironment.Environment.TonemapMode = Godot.Environment.ToneMapper.Aces;

        myEnvironment.Environment.VolumetricFogEnabled = true;
        myEnvironment.Environment.VolumetricFogDensity = 0.02f;
        myEnvironment.Environment.VolumetricFogEmission = new Color(0.15f, 0.15f, 0.15f);

        mySun.DirectionalShadowMaxDistance = 256.0f;
        mySun.Rotation = new Vector3(100.0f, 0.0f, 0.0f);
        mySun.LightColor = (new Color(0.6f, 0.6f, 0.6f));
        mySun.LightEnergy = 0.4f;
        mySun.ShadowEnabled = true;
        mySun.Visible = true;

        //tell weather particles to not do anything
    }

    public void MakeItSunny()
    {
        myEnvironment.Environment.BackgroundMode = Godot.Environment.BGMode.Sky;

        mySkyMaterial.SkyTopColor = new Color(0.2f, 0.3f, 0.5f);
        mySkyMaterial.SkyHorizonColor = new Color(0.5f, 0.6f, 0.7f);
        mySkyMaterial.GroundBottomColor = new Color(0.2f, 0.3f, 0.5f);
        mySkyMaterial.GroundHorizonColor = new Color(0.5f, 0.6f, 0.7f);
        mySkyMaterial.GroundCurve = 0.13f;

        myEnvironment.Environment.Sky.SkyMaterial = mySkyMaterial;
        myEnvironment.Environment.AmbientLightColor = new Color(1f, 1f, 1f);
        myEnvironment.Environment.AmbientLightSkyContribution = 0.5f;
        myEnvironment.Environment.AmbientLightEnergy = 0.2f;
        myEnvironment.Environment.TonemapMode = Godot.Environment.ToneMapper.Aces;

        myEnvironment.Environment.VolumetricFogEnabled = false;

        mySun.DirectionalShadowMaxDistance = 256.0f;
        mySun.Rotation = new Vector3(100.0f, 0.0f, 0.0f);
        mySun.LightColor = (new Color(0.9f, 0.9f, 0.9f));
        mySun.LightEnergy = 1.0f;
        mySun.ShadowEnabled = true;
        mySun.Visible = true;

        //tell weather particles to not do anything
    }
}
