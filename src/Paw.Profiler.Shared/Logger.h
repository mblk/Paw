#pragma once

#include <fstream>
#include <mutex>

#if defined(_MSC_VER)
#define PAW_FORCEINLINE __forceinline
#elif defined(__GNUC__) || defined(__clang__)
#define PAW_FORCEINLINE inline __attribute__((always_inline))
#else
#define PAW_FORCEINLINE inline
#endif

enum class LogLevel {
	Verbose,
	Debug,
	Info,
	Warning,
	Error,
	Fatal
};

class Logger final {
public:
	static Logger& Get();
	static void Shutdown();
	static const char* LogLevelToString(LogLevel level);

	LogLevel GetLevel() const;
	void SetLevel(LogLevel level);

	void Log(LogLevel level, const char* text) {
		if (level < _level)
			return;

		DoLog(level, text);
	}

	template<typename... Args>
	void Log(LogLevel level, Args&&... args) {
		if (level < _level)
			return;

		char buffer[1 << 10];
#ifdef _WINDOWS
		_snprintf_s(buffer, sizeof(buffer), args...);
#else
		std::snprintf(buffer, sizeof(buffer), args...);
#endif
		DoLog(level, buffer);

	}
	template<typename... Args>
	PAW_FORCEINLINE static void Info(Args&&... args) {
		Get().Log(LogLevel::Info, std::forward<Args>(args)...);
	}

	template<typename... Args>
	PAW_FORCEINLINE static void Debug(Args&&... args) {
		Get().Log(LogLevel::Debug, std::forward<Args>(args)...);
	}

	template<typename... Args>
	PAW_FORCEINLINE static void Error(Args&&... args) {
		Get().Log(LogLevel::Error, std::forward<Args>(args)...);
	}

	template<typename... Args>
	PAW_FORCEINLINE static void Warning(Args&&... args) {
		Get().Log(LogLevel::Warning, std::forward<Args>(args)...);
	}

	template<typename... Args>
	PAW_FORCEINLINE static void Verbose(Args&&... args) {
		Get().Log(LogLevel::Verbose, std::forward<Args>(args)...);
	}

private:
	Logger();
	void DoLog(LogLevel level, const char* text);
	void Term();

private:
	//Mutex _lock;
	std::mutex _mutex;

	std::ofstream _file;
	LogLevel _level = LogLevel::Debug;
};
