
#include "OS.h"

#ifdef _WINDOWS
	#include <Windows.h>
#else
	#include <sys/types.h>
	#include <sys/syscall.h>
	#include <unistd.h>
	#include <fcntl.h>
	#include <stdlib.h>
#endif

std::string OS::ReadEnvironmentVariable(const char* name) {
#ifdef _WINDOWS
	char value[1024];
	::GetEnvironmentVariableA(name, value, sizeof(value));
	return value;
#else
	return ::getenv(name);
#endif
}

int OS::GetPid() {
#ifdef _WINDOWS
	return ::GetCurrentProcessId();
#else
	return getpid();
#endif
}

int OS::GetTid() {
#ifdef _WINDOWS
	return ::GetCurrentThreadId();
#else
	return static_cast<int>(syscall(SYS_gettid));
#endif
}

std::string OS::GetCurrentDir() {
	char buffer[512] = { 0 };
#ifdef _WINDOWS
	::GetCurrentDirectoryA(sizeof(buffer), buffer);
#else
	getcwd(buffer, sizeof(buffer));
#endif
	return buffer;
}

std::string OS::UnicodeToAnsi(const WCHAR* str) {
	if (str == nullptr) {
		return {};
	}

#ifdef _WINDOWS
	const int requiredSize = ::WideCharToMultiByte(
		CP_ACP,
		0,
		str,
		-1,
		nullptr,
		0,
		nullptr,
		nullptr);

	if (requiredSize <= 0) {
		return {};
	}

	std::string result(static_cast<size_t>(requiredSize - 1), '\0');

	::WideCharToMultiByte(
		CP_ACP,
		0,
		str,
		-1,
		result.data(),
		requiredSize,
		nullptr,
		nullptr);

	return result;
#else
	std::basic_string<WCHAR> ws(str);
	return std::string(ws.begin(), ws.end());
#endif
}