#include <cstring>
#include <cstdio>

#include "Logger.h"
#include "OS.h"
#include "CoreProfilerFactory.h"

#ifdef _WINDOWS
#define PROFILER_EXPORT extern "C" //__declspec(dllexport)
#else
#define PROFILER_EXPORT extern "C" //__attribute__((visibility("default")))
#endif

//class __declspec(uuid("9F2716B7-F482-45F8-BDD5-867512FB9225")) CoreProfiler;
static const GUID CLSID_CoreProfiler = { 0x9F2716B7, 0xF482, 0x45F8, { 0xBD, 0xD5, 0x86, 0x75, 0x12, 0xFB, 0x92, 0x25 } };

PROFILER_EXPORT BOOL __stdcall DllMain(HINSTANCE hInstDll, DWORD reason, PVOID) {
	switch (reason) {
	case DLL_PROCESS_ATTACH:
		Logger::Info("Profiler DLL loaded into PID %d", OS::GetPid());
		break;

	case DLL_PROCESS_DETACH:
		Logger::Info("Profiler DLL unloaded from PID %d", OS::GetPid());
		Logger::Shutdown();
		break;
	}
	return TRUE;
}

PROFILER_EXPORT HRESULT __stdcall DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv) {
	Logger::Debug(__FUNCTION__);

	//if (rclsid == __uuidof(CoreProfiler)) {
	//if (InlineIsEqualGUID(rclsid, CLSID_CoreProfiler)) {
	if (memcmp(&rclsid, &CLSID_CoreProfiler, sizeof(GUID)) == 0) {
		static CoreProfilerFactory factory;
		return factory.QueryInterface(riid, ppv);
	}

	return CLASS_E_CLASSNOTAVAILABLE;
}