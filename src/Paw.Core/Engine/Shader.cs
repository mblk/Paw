using System.Numerics;

namespace Paw.Core.Engine;

public class ShaderLoader : AssetLoader<Shader>
{
    public ShaderLoader(IAssetManager assetManager, IAssetReader assetReader, GL gl)
        : base(assetManager, assetReader, gl)
    {
    }

    public override AssetLoadResult<Shader> Load(string name)
    {
        var vsPath = Reader.GetAssetPath(AssetType.Shader, $"{name}.vert");
        var fsPath = Reader.GetAssetPath(AssetType.Shader, $"{name}.frag");

        var vsSource = Reader.ReadFileAsString(vsPath);
        var fsSource = Reader.ReadFileAsString(fsPath);

        // process includes etc

        var allFiles = new HashSet<string>
        {
            vsPath,
            fsPath,
            // ...
        };

        var shader = new Shader(GL, name, vsSource, fsSource);

        return new AssetLoadResult<Shader>(shader, allFiles);
    }

    public override AssetLoadResult<Shader> Reload(Shader shader)
    {
        var name = shader.Name;

        var vsPath = Reader.GetAssetPath(AssetType.Shader, $"{name}.vert");
        var fsPath = Reader.GetAssetPath(AssetType.Shader, $"{name}.frag");

        var vsSource = Reader.ReadFileAsString(vsPath);
        var fsSource = Reader.ReadFileAsString(fsPath);

        // process includes etc

        var allFiles = new HashSet<string>
        {
            vsPath,
            fsPath,
            // ...
        };

        shader.Reload(vsSource, fsSource);

        return new AssetLoadResult<Shader>(shader, allFiles);
    }
}

public sealed class Shader : Asset, IDisposable
{
    private readonly GL _gl;
    private readonly string _name;

    private GL.ProgramId _program;

    public string Name => _name;

    public Shader(GL gl, string name, string vsSource, string fsSource)
    {
        _gl = gl;
        _name = name;
        _program = CompileProgram(vsSource, fsSource);
    }

    public void Dispose()
    {
        if (_program != default)
        {
            _gl.DeleteProgram(_program);
            _program = default;
        }
    }

    public void Reload(string vsSource, string fsSource)
    {
        if (_program != default)
        {
            _gl.DeleteProgram(_program);
            _program = default;
        }

        _program = CompileProgram(vsSource, fsSource);
    }

    public void Use()
    {
        _gl.UseProgram(_program);
    }

    public void Unuse()
    {
        _gl.UseProgram(default);
    }

    public void SetUniform(string name, int value)
    {
        int location = _gl.GetUniformLocation(_program, name);
        if (location != -1)
        {
            //_gl.Uniform1i(location, value); // without DSA-like API - program must be bound
            _gl.ProgramUniform1i(_program, location, value); // DSA-like without the need to bind the program first
        }
    }

    public void SetUniform(string name, float value)
    {
        int location = _gl.GetUniformLocation(_program, name);
        if (location != -1)
        {
            _gl.ProgramUniform1f(_program, location, value);
        }
    }

    public void SetUniform(string name, Vector2 value)
    {
        int location = _gl.GetUniformLocation(_program, name);
        if (location != -1)
        {
            _gl.ProgramUniform2f(_program, location, value.X, value.Y);
        }
    }

    public void SetUniform(string name, Vector3 value)
    {
        int location = _gl.GetUniformLocation(_program, name);
        if (location != -1)
        {
            _gl.ProgramUniform3f(_program, location, value.X, value.Y, value.Z); // TODO try *v variants ?
        }
    }

    public unsafe void SetUniform(string name, Matrix4x4 value, bool transpose = false)
    {
        int location = _gl.GetUniformLocation(_program, name);
        if (location != -1)
        {
            float* p = (float*)&value;
            _gl.ProgramUniformMatrix4fv(_program, location, 1, transpose, p);
        }
    }





    private GL.ProgramId CompileProgram(string vsSource, string fsSource)
    {
        Console.WriteLine($"Compiling shader '{_name}' ...");

        GL.ShaderId vs = default, fs = default;
        GL.ProgramId p = default;

        try
        {
            vs = _gl.CreateShader(GL.ShaderType.VERTEX_SHADER);
            _gl.ShaderSource(vs, vsSource);
            _gl.CompileShader(vs);
            CheckShader(vs, "Vertex");

            fs = _gl.CreateShader(GL.ShaderType.FRAGMENT_SHADER);
            _gl.ShaderSource(fs, fsSource);
            _gl.CompileShader(fs);
            CheckShader(fs, "Fragment");

            p = _gl.CreateProgram();
            _gl.AttachShader(p, vs);
            _gl.AttachShader(p, fs);
            _gl.LinkProgram(p);
            CheckProgram(p);

            return p;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in Shader '{_name}': {e.Message}");

            if (p != default)
            {
                _gl.DeleteProgram(p);
            }

            return default;
        }
        finally
        {
            if (vs != default) _gl.DeleteShader(vs);
            if (fs != default) _gl.DeleteShader(fs);
        }
    }

    private void CheckShader(GL.ShaderId shader, string label)
    {
        int status = _gl.GetShaderI(shader, GL.ShaderParameterName.COMPILE_STATUS);
        if (status == 0)
        {
            var infoLog = _gl.GetShaderInfoLog(shader);
            throw new Exception($"{label} compile error: {infoLog}");
        }
    }

    private void CheckProgram(GL.ProgramId prog)
    {
        int status = _gl.GetProgramI(prog, GL.ProgramProperty.LINK_STATUS);
        if (status == 0)
        {
            var infoLog = _gl.GetProgramInfoLog(prog);
            throw new Exception($"Link error: {infoLog}");
        }
    }
}


