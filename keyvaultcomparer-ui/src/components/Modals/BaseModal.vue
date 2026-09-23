<script setup lang="ts">
const props = defineProps<{
  show: boolean;
  title: string;
  maxWidth?: string;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in" @click="emit('close')">
    <div class="bg-white rounded-xl shadow-xl w-full overflow-hidden transform transition-all flex flex-col" :class="maxWidth || 'max-w-md'" @click.stop>
      <!-- Header -->
      <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50">
        <h3 class="text-lg font-bold text-slate-900">{{ title }}</h3>
        <button @click="emit('close')" class="text-slate-400 hover:text-slate-600 transition-colors p-1 rounded-md hover:bg-slate-200">
          <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
      
      <!-- Body -->
      <div class="p-6 overflow-y-auto max-h-[70vh]">
        <slot></slot>
      </div>

      <!-- Footer -->
      <div v-if="$slots.footer" class="px-6 py-4 bg-slate-50 border-t border-slate-100 flex justify-end gap-3 rounded-b-xl">
        <slot name="footer"></slot>
      </div>
    </div>
  </div>
</template>
