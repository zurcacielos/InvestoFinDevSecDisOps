<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import type { UserProfile } from '../../stores/authStore';

const props = withDefaults(
  defineProps<{
    profile?: UserProfile | null;
    cloudAuthMethod?: string | null;
    size?: 'sm' | 'md' | 'lg';
    showDetails?: boolean;
  }>(),
  {
    profile: null,
    cloudAuthMethod: 'Azure SSO',
    size: 'md',
    showDetails: false,
  }
);

const emit = defineEmits<{
  (e: 'sign-in'): void;
}>();

const imgFailed = ref(false);

watch(
  () => props.profile?.photoUrl,
  () => {
    imgFailed.value = false;
  }
);

const isConnected = computed(() => {
  return !!(props.profile && props.profile.email && props.profile.email !== 'Unknown User');
});

const sizeClasses = computed(() => {
  switch (props.size) {
    case 'sm':
      return {
        container: 'w-6 h-6 text-xs',
        statusDot: 'w-1.5 h-1.5 -bottom-0.5 -right-0.5 border',
      };
    case 'lg':
      return {
        container: 'w-10 h-10 text-base',
        statusDot: 'w-3 h-3 -bottom-0.5 -right-0.5 border-2',
      };
    case 'md':
    default:
      return {
        container: 'w-8 h-8 text-xs',
        statusDot: 'w-2.5 h-2.5 -bottom-0.5 -right-0.5 border-2',
      };
  }
});

const tooltipText = computed(() => {
  if (!isConnected.value) {
    return 'Not connected to Azure Cloud. Click to sign in.';
  }
  const email = props.profile?.email || 'Authenticated User';
  const sub = props.profile?.subscriptionName ? ` (${props.profile.subscriptionName})` : '';
  const method = props.cloudAuthMethod ? ` via ${props.cloudAuthMethod}` : '';
  return `Connected as ${email}${sub}${method}`;
});
</script>

<template>
  <div class="inline-flex items-center gap-2.5 select-none">
    <!-- Authenticated User Avatar -->
    <template v-if="isConnected && profile">
      <div 
        class="relative shrink-0 flex items-center justify-center cursor-default group"
        :class="sizeClasses.container"
        :title="tooltipText"
      >
        <!-- Real Photo (Microsoft Graph / ADO) -->
        <img 
          v-if="profile.photoUrl && !imgFailed"
          :src="profile.photoUrl"
          @error="imgFailed = true"
          :alt="profile.email"
          class="w-full h-full rounded-full object-cover shadow-xs ring-1 ring-slate-300 dark:ring-slate-700 bg-slate-100 transition-transform group-hover:scale-105"
          loading="lazy"
        />

        <!-- Fallback Initials Badge -->
        <div 
          v-else
          class="w-full h-full rounded-full bg-gradient-to-br from-blue-600 to-indigo-700 text-white flex items-center justify-center font-bold shadow-xs ring-1 ring-blue-500/30 transition-transform group-hover:scale-105"
        >
          {{ profile.initials || '??' }}
        </div>

        <!-- Online Status Indicator Dot -->
        <span 
          class="absolute rounded-full bg-emerald-500 border-white dark:border-slate-900 shadow-2xs pointer-events-none"
          :class="sizeClasses.statusDot"
        ></span>
      </div>

      <!-- Optional Inline Text Details (e.g. for modals or settings) -->
      <div v-if="showDetails" class="min-w-0 text-left">
        <div class="text-xs font-semibold text-slate-900 dark:text-slate-100 truncate">
          {{ profile.email }}
        </div>
        <div class="text-[11px] text-slate-500 truncate">
          {{ profile.subscriptionName || 'Default Subscription' }}
        </div>
      </div>
    </template>

    <!-- Disconnected State -->
    <template v-else>
      <button 
        type="button"
        @click="emit('sign-in')"
        class="flex items-center gap-1.5 px-3 py-1 text-xs font-semibold rounded-full bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 border border-slate-300 dark:border-slate-600 transition-colors shadow-2xs cursor-pointer focus:outline-none"
        title="Sign in with your corporate Microsoft account"
      >
        <span class="w-2 h-2 rounded-full bg-rose-500 animate-pulse"></span>
        <span>Azure: Disconnected</span>
      </button>
    </template>
  </div>
</template>
