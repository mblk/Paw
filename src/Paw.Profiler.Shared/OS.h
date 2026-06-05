#pragma once

#include <string>

#ifdef _WINDOWS
	#include <Windows.h>
#else
	#include <pal_mstypes.h>
#endif

class OS final {
public:
	static std::string ReadEnvironmentVariable(const char* name);
	static int GetPid();
	static int GetTid();
	static std::string GetCurrentDir();
	static std::string UnicodeToAnsi(const WCHAR* str);
};
