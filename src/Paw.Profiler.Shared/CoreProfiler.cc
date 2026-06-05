
#include "Logger.h"

#include <vector>
#include <string>
#include <cassert>
#include <cstdio>

#ifndef _WINDOWS

	#define INITGUID
	#include <guiddef.h>

	DEFINE_GUID(IID_IUnknown, 0x00000000, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
	DEFINE_GUID(IID_IClassFactory, 0x00000001, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);

	static bool minipal_guid_equals(GUID const* g1, GUID const* g2)
	{
		return memcmp(g1, g2, sizeof(GUID)) == 0;
	}

#endif

#include "CoreProfiler.h"
#include "OS.h"

#define HR(x) { auto _hr = (x); if(FAILED(_hr)) return _hr; }

#ifdef _WINDOWS
	#define PAW_PRINTF printf_s
#else
	#define PAW_PRINTF printf
#endif

struct StackSnapshotContext {
	std::vector<std::string>* frames;
	CoreProfiler* profiler;
};

HRESULT __stdcall CoreProfiler::QueryInterface(REFIID riid, void** ppvObject) {
	if (ppvObject == nullptr)
		return E_POINTER;

	if (riid == __uuidof(IUnknown) ||
		riid == __uuidof(ICorProfilerCallback) ||
		riid == __uuidof(ICorProfilerCallback2) ||
		riid == __uuidof(ICorProfilerCallback3) ||
		riid == __uuidof(ICorProfilerCallback4) ||
		riid == __uuidof(ICorProfilerCallback5) ||
		riid == __uuidof(ICorProfilerCallback6) ||
		riid == __uuidof(ICorProfilerCallback7) ||
		riid == __uuidof(ICorProfilerCallback8)) {
		AddRef();
		*ppvObject = static_cast<ICorProfilerCallback8*>(this);
		return S_OK;
	}

	return E_NOINTERFACE;
}

ULONG __stdcall CoreProfiler::AddRef(void) {
	return ++_refCount;
}

ULONG __stdcall CoreProfiler::Release(void) {
	auto count = --_refCount;
	if (count == 0)
		delete this;

	return count;
}

HRESULT CoreProfiler::Initialize(IUnknown* pICorProfilerInfoUnk) {
	Logger::Debug(__FUNCTION__);

	_info = nullptr;
	//pICorProfilerInfoUnk->QueryInterface(&_info);
	HR(pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo8), reinterpret_cast<void**>(&_info)));
	assert(_info);

	assert(_info);

	_info->SetEventMask(
		COR_PRF_MONITOR_ASSEMBLY_LOADS |
		COR_PRF_MONITOR_MODULE_LOADS |
		COR_PRF_MONITOR_CLASS_LOADS |

		COR_PRF_MONITOR_JIT_COMPILATION |

		COR_PRF_MONITOR_GC |
		COR_PRF_MONITOR_THREADS |
		COR_PRF_MONITOR_EXCEPTIONS |

		COR_PRF_ENABLE_STACK_SNAPSHOT |
		COR_PRF_MONITOR_OBJECT_ALLOCATED |
		COR_PRF_ENABLE_OBJECT_ALLOCATED
	);

	return S_OK;
}

HRESULT CoreProfiler::Shutdown() {
	Logger::Info("Profiler shutdown (PID=%d)", OS::GetPid());
	_info->Release();

	return S_OK;
}

HRESULT CoreProfiler::AppDomainCreationStarted(AppDomainID appDomainId) {
	return S_OK;
}

HRESULT CoreProfiler::AppDomainCreationFinished(AppDomainID appDomainId, HRESULT hrStatus) {
	return S_OK;
}

HRESULT CoreProfiler::AppDomainShutdownStarted(AppDomainID appDomainId) {
	return S_OK;
}

HRESULT CoreProfiler::AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus) {
	return S_OK;
}

HRESULT CoreProfiler::AssemblyLoadStarted(AssemblyID assemblyId) {
	return S_OK;
}

HRESULT CoreProfiler::AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus) {
	WCHAR name[512];
	ULONG size;
	AppDomainID ad;
	ModuleID module;
	if (SUCCEEDED(_info->GetAssemblyInfo(assemblyId, sizeof(name) / sizeof(name[0]), &size, name, &ad, &module))) {
		Logger::Info("Assembly loaded: %s (id=0x%p)", OS::UnicodeToAnsi(name).c_str(), assemblyId);
	}

	return S_OK;
}

HRESULT CoreProfiler::AssemblyUnloadStarted(AssemblyID assemblyId) {
	return S_OK;
}

HRESULT CoreProfiler::AssemblyUnloadFinished(AssemblyID assemblyId, HRESULT hrStatus) {
	return S_OK;
}

HRESULT CoreProfiler::ModuleLoadStarted(ModuleID moduleId) {
	return S_OK;
}

HRESULT CoreProfiler::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus) {

	LPCBYTE pBaseLoadAdress;
	WCHAR name[512];
	ULONG size;
	AssemblyID assembly;

	if (FAILED(_info->GetModuleInfo(moduleId, &pBaseLoadAdress, 512, &size, name, &assembly))) {
		Logger::Error("GetModuleInfo failed");
		return S_OK;
	}

	Logger::Info("Module loaded: %s (id=0x%p, assembly=0x%p) at base addr 0x%p",
		OS::UnicodeToAnsi(name).c_str(), moduleId, assembly, pBaseLoadAdress);

	return S_OK;
}

HRESULT CoreProfiler::ModuleUnloadStarted(ModuleID moduleId) {
	return S_OK;
}

HRESULT CoreProfiler::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus) {
	return S_OK;
}

HRESULT CoreProfiler::ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID AssemblyId) {
	return S_OK;
}

HRESULT CoreProfiler::ClassLoadStarted(ClassID classId) {
	return S_OK;
}

HRESULT CoreProfiler::ClassLoadFinished(ClassID classId, HRESULT hrStatus) {

	std::string className = GetClassName(classId);
	Logger::Debug("Type %s loaded", className.c_str());

	return S_OK;
}

HRESULT CoreProfiler::ClassUnloadStarted(ClassID classId) {
	return S_OK;
}

HRESULT CoreProfiler::ClassUnloadFinished(ClassID classId, HRESULT hrStatus) {
	return S_OK;
}

HRESULT CoreProfiler::FunctionUnloadStarted(FunctionID functionId) {
	return S_OK;
}

HRESULT CoreProfiler::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock) {
	Logger::Debug("JIT compilation started: %s", GetMethodName(functionId).c_str());

	return S_OK;
}

HRESULT CoreProfiler::JITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock) {
	Logger::Debug("JIT compilation finished: %s", GetMethodName(functionId).c_str());

	return S_OK;
}

HRESULT CoreProfiler::JITCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction) {
	return S_OK;
}

HRESULT CoreProfiler::JITCachedFunctionSearchFinished(FunctionID functionId, COR_PRF_JIT_CACHE result) {
	return S_OK;
}

HRESULT CoreProfiler::JITFunctionPitched(FunctionID functionId) {
	return S_OK;
}

HRESULT CoreProfiler::JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline) {
	return S_OK;
}

HRESULT CoreProfiler::ThreadCreated(ThreadID threadId) {
	Logger::Info("Thread 0x%p created", threadId);

	return S_OK;
}

HRESULT CoreProfiler::ThreadDestroyed(ThreadID threadId) {
	Logger::Info("Thread 0x%p destroyed", threadId);

	return S_OK;
}

HRESULT CoreProfiler::ThreadAssignedToOSThread(ThreadID managedThreadId, DWORD osThreadId) {
	Logger::Info("Thread 0x%p assigned to OS thread %d", managedThreadId, osThreadId);
	return S_OK;
}

HRESULT CoreProfiler::RemotingClientInvocationStarted() {
	return S_OK;
}

HRESULT CoreProfiler::RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync) {
	return S_OK;
}

HRESULT CoreProfiler::RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync) {
	return S_OK;
}

HRESULT CoreProfiler::RemotingClientInvocationFinished() {
	return S_OK;
}

HRESULT CoreProfiler::RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync) {
	return S_OK;
}

HRESULT CoreProfiler::RemotingServerInvocationStarted() {
	return S_OK;
}

HRESULT CoreProfiler::RemotingServerInvocationReturned() {
	return S_OK;
}

HRESULT CoreProfiler::RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync) {
	return S_OK;
}

HRESULT CoreProfiler::UnmanagedToManagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) {
	Logger::Verbose(__FUNCTION__);
	return S_OK;
}

HRESULT CoreProfiler::ManagedToUnmanagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) {
	Logger::Verbose(__FUNCTION__);
	return S_OK;
}

HRESULT CoreProfiler::RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason) {
	return S_OK;
}

HRESULT CoreProfiler::RuntimeSuspendFinished() {
	return S_OK;
}

HRESULT CoreProfiler::RuntimeSuspendAborted() {
	return S_OK;
}

HRESULT CoreProfiler::RuntimeResumeStarted() {
	return S_OK;
}

HRESULT CoreProfiler::RuntimeResumeFinished() {
	return S_OK;
}

HRESULT CoreProfiler::RuntimeThreadSuspended(ThreadID threadId) {
	return S_OK;
}

HRESULT CoreProfiler::RuntimeThreadResumed(ThreadID threadId) {
	return S_OK;
}

HRESULT CoreProfiler::MovedReferences(ULONG cMovedObjectIDRanges, ObjectID* oldObjectIDRangeStart, ObjectID* newObjectIDRangeStart, ULONG* cObjectIDRangeLength) {
	return S_OK;
}

HRESULT CoreProfiler::ObjectAllocated(ObjectID objectId, ClassID classId) {

	ULONG objectSize;
	if (FAILED(_info->GetObjectSize(objectId, &objectSize))) {
		objectSize = 0;
	}

	CorElementType arrayBaseType;
	ClassID arrayBaseClass;
	ULONG arrayRank;

	if (SUCCEEDED(_info->IsArrayClass(classId, &arrayBaseType, &arrayBaseClass, &arrayRank)) && arrayBaseClass != 0) {
		std::string arrayTypeName = GetClassName(arrayBaseClass);
		PAW_PRINTF("Allocated array of type '%s' (0x%x)(%u bytes)\n", arrayTypeName.c_str(), (unsigned int)arrayBaseType, objectSize);
		return S_OK;
	}

	// TODO try _info->GetClassIDInfo2()
	std::string typeName = GetClassName(classId);

	ThreadID threadId;
	if (FAILED(_info->GetCurrentThreadID(&threadId))) {
		Logger::Error("GetCurrentThreadID failed");
		return S_OK;
	}

	std::vector<std::string> frames;

	StackSnapshotContext context = {
		.frames = &frames,
		.profiler = this,
	};

	if (FAILED(_info->DoStackSnapshot(threadId, StackSnapshotCB, 0, &context, nullptr, 0))) {
		Logger::Error("DoStackSnapshot failed");
		return S_OK;
	}

	auto it = _threadNames.find(threadId);
	const char* threadName = it != _threadNames.end() ? it->second.c_str() : "<unnamed>";

	Logger::Debug("Allocated object 0x%p of type %s (%u bytes) on managed thread 0x%p (%s)",
				  objectId, typeName.c_str(), objectSize, threadId, threadName);

	for (const auto& frame : frames) {
		Logger::Debug("  at %s", frame.c_str());
	}

	PAW_PRINTF("Allocated object of type '%s' (%u bytes)\n", typeName.c_str(), objectSize);

	/*for (const auto& frame : frames) {
		PAW_PRINTF("    at %s\n", frame.c_str());
	}*/

	return S_OK;
}

HRESULT CoreProfiler::ObjectsAllocatedByClass(ULONG cClassCount, ClassID* classIds, ULONG* cObjects) {
	return S_OK;
}

HRESULT CoreProfiler::ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs, ObjectID* objectRefIds) {
	return S_OK;
}

HRESULT CoreProfiler::RootReferences(ULONG cRootRefs, ObjectID* rootRefIds) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionThrown(ObjectID thrownObjectId) {

	ClassID classId;
	HR(_info->GetClassFromObject(thrownObjectId, &classId));

	Logger::Warning("Exception %s thrown", GetClassName(classId).c_str());

	//std::vector<std::string> data;
	//if (SUCCEEDED(_info->DoStackSnapshot(0, StackSnapshotCB, 0, &data, nullptr, 0))) {
	//	// TODO
	//}

	return S_OK;
}

HRESULT CoreProfiler::ExceptionSearchFunctionEnter(FunctionID functionId) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionSearchFunctionLeave() {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionSearchFilterEnter(FunctionID functionId) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionSearchFilterLeave() {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionSearchCatcherFound(FunctionID functionId) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionOSHandlerEnter(UINT_PTR __unused) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionOSHandlerLeave(UINT_PTR __unused) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionUnwindFunctionEnter(FunctionID functionId) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionUnwindFunctionLeave() {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionUnwindFinallyEnter(FunctionID functionId) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionUnwindFinallyLeave() {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionCatcherLeave() {
	return S_OK;
}

HRESULT CoreProfiler::COMClassicVTableCreated(ClassID wrappedClassId, const GUID& implementedIID, void* pVTable, ULONG cSlots) {
	return S_OK;
}

HRESULT CoreProfiler::COMClassicVTableDestroyed(ClassID wrappedClassId, const GUID& implementedIID, void* pVTable) {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionCLRCatcherFound() {
	return S_OK;
}

HRESULT CoreProfiler::ExceptionCLRCatcherExecute() {
	return S_OK;
}

HRESULT CoreProfiler::ThreadNameChanged(ThreadID threadId, ULONG cchName, WCHAR* name) {
	if (name == nullptr || cchName == 0) {
		Logger::Error("ThreadNameChanged error");
		_threadNames[threadId] = "";
		return S_OK;
	}

	_threadNames[threadId] = OS::UnicodeToAnsi(name);
	Logger::Info("ThreadNameChanged: Thread 0x%p renamed to %s", threadId, _threadNames[threadId].c_str());
	return S_OK;
}

HRESULT CoreProfiler::GarbageCollectionStarted(int cGenerations, BOOL* generationCollected, COR_PRF_GC_REASON reason) {
	Logger::Debug(__FUNCTION__);
	Logger::Info("GC started. Gen0=%s, Gen1=%s, Gen2=%s",
		generationCollected[0] ? "Yes" : "No", generationCollected[1] ? "Yes" : "No", generationCollected[2] ? "Yes" : "No");

	return S_OK;
}

HRESULT CoreProfiler::SurvivingReferences(ULONG cSurvivingObjectIDRanges, ObjectID* objectIDRangeStart, ULONG* cObjectIDRangeLength) {
	return S_OK;
}

HRESULT CoreProfiler::GarbageCollectionFinished() {
	Logger::Info("GC finished");

	return S_OK;
}

HRESULT CoreProfiler::FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID) {
	return S_OK;
}

HRESULT CoreProfiler::RootReferences2(ULONG cRootRefs, ObjectID* rootRefIds, COR_PRF_GC_ROOT_KIND* rootKinds, COR_PRF_GC_ROOT_FLAGS* rootFlags, UINT_PTR* rootIds) {
	return S_OK;
}

HRESULT CoreProfiler::HandleCreated(GCHandleID handleId, ObjectID initialObjectId) {
	return S_OK;
}

HRESULT CoreProfiler::HandleDestroyed(GCHandleID handleId) {
	return S_OK;
}

HRESULT CoreProfiler::InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData, UINT cbClientData) {
	return S_OK;
}

HRESULT CoreProfiler::ProfilerAttachComplete() {
	return S_OK;
}

HRESULT CoreProfiler::ProfilerDetachSucceeded() {
	return S_OK;
}

HRESULT CoreProfiler::ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId, BOOL fIsSafeToBlock) {
	return S_OK;
}

HRESULT CoreProfiler::GetReJITParameters(ModuleID moduleId, mdMethodDef methodId, ICorProfilerFunctionControl* pFunctionControl) {
	return S_OK;
}

HRESULT CoreProfiler::ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId, HRESULT hrStatus, BOOL fIsSafeToBlock) {
	return S_OK;
}

HRESULT CoreProfiler::ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId, HRESULT hrStatus) {
	return S_OK;
}

HRESULT CoreProfiler::MovedReferences2(ULONG cMovedObjectIDRanges, ObjectID* oldObjectIDRangeStart, ObjectID* newObjectIDRangeStart, SIZE_T* cObjectIDRangeLength) {
	return S_OK;
}

HRESULT CoreProfiler::SurvivingReferences2(ULONG cSurvivingObjectIDRanges, ObjectID* objectIDRangeStart, SIZE_T* cObjectIDRangeLength) {
	return S_OK;
}

HRESULT CoreProfiler::ConditionalWeakTableElementReferences(ULONG cRootRefs, ObjectID* keyRefIds, ObjectID* valueRefIds, GCHandleID* rootIds) {
	return S_OK;
}

HRESULT CoreProfiler::GetAssemblyReferences(const WCHAR* wszAssemblyPath, ICorProfilerAssemblyReferenceProvider* pAsmRefProvider) {
	return S_OK;
}

HRESULT CoreProfiler::ModuleInMemorySymbolsUpdated(ModuleID moduleId) {
	return S_OK;
}

HRESULT CoreProfiler::DynamicMethodJITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock, LPCBYTE pILHeader, ULONG cbILHeader) {
	return S_OK;
}

HRESULT CoreProfiler::DynamicMethodJITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock) {
	return S_OK;
}

std::string CoreProfiler::GetClassName(ClassID classId) const {
	ModuleID module;
	mdTypeDef type;

	if (FAILED(_info->GetClassIDInfo(classId, &module, &type))) {
		return "<unknown 1>";
	}

	if (module == 0 || type == mdTypeDefNil) {
		return "<unknown 2>";
	}

	//CComPtr<IMetaDataImport> spMetadata;
	IMetaDataImport* spMetadata;

	HRESULT hr;

	hr = _info->GetModuleMetaData(module, ofRead, IID_IMetaDataImport, reinterpret_cast<IUnknown**>(&spMetadata));
	if (FAILED(hr)) {
		return "<unknown 3>";
	}

	WCHAR name[256];
	ULONG nameSize = 256;
	DWORD flags;
	mdTypeDef baseType;

	hr = spMetadata->GetTypeDefProps(type, name, 256, &nameSize, &flags, &baseType);
	if (FAILED(hr)) {
		return "<unknown 4>";
	}

	return OS::UnicodeToAnsi(name);
}

std::string CoreProfiler::GetMethodName(FunctionID function) const {
	ModuleID module;
	mdToken token;
	mdTypeDef type;
	ClassID classId;
	if (FAILED(_info->GetFunctionInfo(function, &classId, &module, &token)))
		return "<unknown 5>";

	//CComPtr<IMetaDataImport> spMetadata;
	IMetaDataImport* spMetadata;
	if (FAILED(_info->GetModuleMetaData(module, ofRead, IID_IMetaDataImport, reinterpret_cast<IUnknown**>(&spMetadata))))
		return "<unknown 6>";

	PCCOR_SIGNATURE sig;
	ULONG blobSize, size, attributes;
	WCHAR name[256];
	DWORD flags;
	ULONG codeRva;
	if (FAILED(spMetadata->GetMethodProps(token, &type, name, 256, &size, &attributes, &sig, &blobSize, &codeRva, &flags)))
		return "<unknown 7>";

	return GetClassName(classId) + "::" + OS::UnicodeToAnsi(name);
}

HRESULT __stdcall CoreProfiler::StackSnapshotCB(FunctionID funcId, UINT_PTR ip, COR_PRF_FRAME_INFO frameInfo, ULONG32 contextSize, BYTE context[], void* clientData) {

	assert(clientData);

	StackSnapshotContext* stackSnapshotContext = static_cast<StackSnapshotContext*>(clientData);
	std::vector<std::string>* frames = stackSnapshotContext->frames;
	CoreProfiler* profiler = stackSnapshotContext->profiler;

	assert(frames);
	assert(profiler);

	if (funcId == 0) {
		frames->push_back("<runtime/unmanaged>");
		return S_OK;
	}

	std::string methodName = profiler->GetMethodName(funcId);

	if (methodName.empty()) {
		frames->push_back("<unknown>");
	}
	else {
		frames->push_back(methodName);
	}

	return S_OK;
}
