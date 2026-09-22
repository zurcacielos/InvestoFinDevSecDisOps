<script setup lang="ts">
const props = defineProps<{
  show: boolean;
}>();

const emit = defineEmits<{
  (e: 'retry'): void;
}>();
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm">
    <div class="bg-white rounded-xl shadow-xl w-full max-w-md overflow-hidden transform transition-all">
      <div class="p-6">
        <div class="flex items-center gap-2 mb-4 text-rose-600">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
          <h3 class="text-xl font-bold text-slate-900">Autenticación Requerida</h3>
        </div>
        <p class="text-slate-600 mb-6">
          No tenemos conexión con Azure (Tu sesión ha expirado o no fue encontrada). Para continuar, abre la terminal donde está corriendo el servidor y ejecuta:
        </p>
        <div class="bg-slate-100 rounded-lg p-3 flex items-center justify-between mb-6 border border-slate-200">
          <code class="text-slate-800 font-mono text-sm font-semibold">az login</code>
        </div>
        <p class="text-slate-500 text-sm mb-6">
          Alternativamente, puedes apagar el servidor de backend y frontend, y volver a iniciar todo usando el script <code class="font-mono text-slate-700 bg-slate-100 px-1 rounded">start-all.ps1</code>. Una vez conectado, presiona Reintentar.
        </p>
        <div class="flex justify-end">
          <button 
            @click="emit('retry')" 
            class="bg-blue-600 hover:bg-blue-700 text-white font-medium py-2 px-4 rounded-lg transition-colors shadow-sm"
          >
            Reintentar
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
