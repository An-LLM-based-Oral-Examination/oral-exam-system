import { useEffect, useRef } from 'react'
import * as THREE from 'three'

export type ExamSceneState = 'waiting' | 'checking' | 'ready'

interface OfficialExamThreeSceneProps {
  state?: ExamSceneState
  roomLabel: string
  kioskNumber: number
}

interface FloatingNode {
  mesh: THREE.Mesh
  phase: number
  baseScale: number
}

function createSeededRandom(seed = 41_227) {
  let currentSeed = seed

  return () => {
    currentSeed = (currentSeed * 16_807) % 2_147_483_647
    return (currentSeed - 1) / 2_147_483_646
  }
}

function createScreenTexture(roomLabel: string, kioskNumber: number) {
  const canvas = document.createElement('canvas')
  canvas.width = 512
  canvas.height = 320
  const context = canvas.getContext('2d')

  if (!context) return null

  const gradient = context.createLinearGradient(0, 0, 512, 320)
  gradient.addColorStop(0, '#071c48')
  gradient.addColorStop(0.58, '#0b3f80')
  gradient.addColorStop(1, '#0a6c91')
  context.fillStyle = gradient
  context.fillRect(0, 0, canvas.width, canvas.height)

  context.strokeStyle = 'rgba(103, 232, 249, 0.18)'
  context.lineWidth = 1
  for (let x = 0; x <= canvas.width; x += 32) {
    context.beginPath()
    context.moveTo(x, 0)
    context.lineTo(x, canvas.height)
    context.stroke()
  }
  for (let y = 0; y <= canvas.height; y += 32) {
    context.beginPath()
    context.moveTo(0, y)
    context.lineTo(canvas.width, y)
    context.stroke()
  }

  context.fillStyle = '#80f5ff'
  context.font = '700 28px system-ui, sans-serif'
  context.fillText(`KIOSK ${kioskNumber}`, 34, 56)
  context.fillStyle = '#ffffff'
  context.font = '800 78px system-ui, sans-serif'
  context.fillText(roomLabel.replace(/^Lab\s+/i, ''), 32, 158)
  context.fillStyle = '#5eead4'
  context.beginPath()
  context.arc(42, 238, 8, 0, Math.PI * 2)
  context.fill()
  context.fillStyle = '#ccfbf1'
  context.font = '600 22px system-ui, sans-serif'
  context.fillText('SECURE LAB ONLINE', 62, 246)

  const texture = new THREE.CanvasTexture(canvas)
  texture.colorSpace = THREE.SRGBColorSpace
  texture.anisotropy = 4
  return texture
}

export function OfficialExamThreeScene({
  state = 'waiting',
  roomLabel,
  kioskNumber,
}: OfficialExamThreeSceneProps) {
  const mountRef = useRef<HTMLDivElement>(null)
  const stateRef = useRef(state)

  useEffect(() => {
    stateRef.current = state
  }, [state])

  useEffect(() => {
    const mount = mountRef.current
    if (!mount) return

    const random = createSeededRandom()
    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 60)
    camera.position.set(0, 2.25, 8.6)
    camera.lookAt(0, 0.45, 0)

    let renderer: THREE.WebGLRenderer

    try {
      renderer = new THREE.WebGLRenderer({
        alpha: true,
        antialias: true,
        powerPreference: 'high-performance',
      })
    } catch {
      return
    }

    renderer.setClearColor(0x000000, 0)
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 1.03
    renderer.domElement.setAttribute('aria-hidden', 'true')
    renderer.domElement.style.display = 'block'
    renderer.domElement.style.width = '100%'
    renderer.domElement.style.height = '100%'
    mount.appendChild(renderer.domElement)

    const hemisphereLight = new THREE.HemisphereLight(0xf0fdff, 0x07152f, 1.65)
    const keyLight = new THREE.DirectionalLight(0xe5fbff, 3.4)
    const cyanLight = new THREE.PointLight(0x56e8f4, 13, 10, 1.7)
    const blueLight = new THREE.PointLight(0x2e62eb, 10, 9, 1.8)
    const mintLight = new THREE.PointLight(0x47e3b1, 6, 7, 1.7)
    keyLight.position.set(4.5, 6, 5)
    cyanLight.position.set(2.6, 2.4, 3.1)
    blueLight.position.set(-3.2, 0.6, 2.4)
    mintLight.position.set(0.5, 1.2, 3.3)
    scene.add(hemisphereLight, keyLight, cyanLight, blueLight, mintLight)

    const rootGroup = new THREE.Group()
    rootGroup.position.set(0.1, -0.35, 0)
    rootGroup.rotation.x = -0.015
    scene.add(rootGroup)

    const navyMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x071c43,
      emissive: 0x051634,
      emissiveIntensity: 0.22,
      metalness: 0.68,
      roughness: 0.25,
      clearcoat: 0.82,
      clearcoatRoughness: 0.14,
    })
    const blueMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x1f5bd7,
      emissive: 0x0d378d,
      emissiveIntensity: 0.38,
      metalness: 0.58,
      roughness: 0.21,
      clearcoat: 0.9,
      clearcoatRoughness: 0.12,
    })
    const cyanMaterial = new THREE.MeshStandardMaterial({
      color: 0x75f1f7,
      emissive: 0x24bccc,
      emissiveIntensity: 0.72,
      metalness: 0.3,
      roughness: 0.18,
    })
    const mintMaterial = new THREE.MeshStandardMaterial({
      color: 0x64e8bd,
      emissive: 0x24a77f,
      emissiveIntensity: 0.62,
      metalness: 0.28,
      roughness: 0.2,
    })
    const glassMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x86ecff,
      opacity: 0.18,
      transparent: true,
      transmission: 0.34,
      roughness: 0.08,
      metalness: 0.08,
      depthWrite: false,
    })

    const platform = new THREE.Mesh(
      new THREE.CylinderGeometry(3.05, 3.35, 0.24, 72),
      new THREE.MeshPhysicalMaterial({
        color: 0x0b2a61,
        emissive: 0x071f50,
        emissiveIntensity: 0.36,
        metalness: 0.74,
        roughness: 0.24,
        clearcoat: 0.8,
      }),
    )
    platform.position.y = -1.18
    rootGroup.add(platform)

    const platformGlow = new THREE.Mesh(
      new THREE.TorusGeometry(2.92, 0.045, 10, 96),
      new THREE.MeshBasicMaterial({
        color: 0x62eaf2,
        transparent: true,
        opacity: 0.55,
        depthWrite: false,
      }),
    )
    platformGlow.rotation.x = Math.PI / 2
    platformGlow.position.y = -1.04
    rootGroup.add(platformGlow)

    const grid = new THREE.GridHelper(6.3, 18, 0x2f88c4, 0x19486f)
    grid.position.y = -1.045
    const gridMaterials = Array.isArray(grid.material) ? grid.material : [grid.material]
    gridMaterials.forEach((material) => {
      material.transparent = true
      material.opacity = 0.18
      material.depthWrite = false
    })
    rootGroup.add(grid)

    const screenTexture = createScreenTexture(roomLabel, kioskNumber)
    const screenMaterial = new THREE.MeshBasicMaterial({
      color: 0xffffff,
      map: screenTexture,
      toneMapped: false,
    })

    const workstation = new THREE.Group()
    workstation.position.set(0, -0.02, 0.35)
    rootGroup.add(workstation)

    const monitorBack = new THREE.Mesh(new THREE.BoxGeometry(2.45, 1.42, 0.22), navyMaterial)
    monitorBack.position.y = 0.48
    workstation.add(monitorBack)

    const screen = new THREE.Mesh(new THREE.PlaneGeometry(2.19, 1.18), screenMaterial)
    screen.position.set(0, 0.48, 0.118)
    workstation.add(screen)

    const screenFrame = new THREE.Mesh(new THREE.BoxGeometry(2.57, 0.06, 0.26), cyanMaterial)
    screenFrame.position.set(0, -0.24, 0)
    workstation.add(screenFrame)

    const stand = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.68, 0.22), blueMaterial)
    stand.position.y = -0.62
    workstation.add(stand)
    const standBase = new THREE.Mesh(new THREE.BoxGeometry(1.15, 0.13, 0.62), navyMaterial)
    standBase.position.y = -0.98
    workstation.add(standBase)

    const keyboard = new THREE.Mesh(new THREE.BoxGeometry(1.72, 0.09, 0.62), blueMaterial)
    keyboard.position.set(0, -0.9, 0.82)
    keyboard.rotation.x = -0.13
    workstation.add(keyboard)

    const keyboardGlow = new THREE.Mesh(
      new THREE.PlaneGeometry(1.48, 0.4),
      new THREE.MeshBasicMaterial({
        color: 0x76eff7,
        opacity: 0.22,
        transparent: true,
        depthWrite: false,
      }),
    )
    keyboardGlow.position.set(0, -0.84, 1.15)
    keyboardGlow.rotation.x = -0.95
    workstation.add(keyboardGlow)

    const micGroup = new THREE.Group()
    micGroup.position.set(-1.62, -0.38, 0.65)
    workstation.add(micGroup)
    const micBody = new THREE.Mesh(new THREE.CapsuleGeometry(0.22, 0.42, 8, 18), cyanMaterial)
    micBody.position.y = 0.48
    micGroup.add(micBody)
    const micStem = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.06, 0.58, 18), blueMaterial)
    micStem.position.y = -0.02
    micGroup.add(micStem)
    const micBase = new THREE.Mesh(new THREE.CylinderGeometry(0.42, 0.5, 0.11, 32), navyMaterial)
    micBase.position.y = -0.35
    micGroup.add(micBase)

    const shieldGroup = new THREE.Group()
    shieldGroup.position.set(1.68, 0.3, 0.7)
    workstation.add(shieldGroup)
    const shield = new THREE.Mesh(new THREE.OctahedronGeometry(0.42, 1), glassMaterial)
    shield.scale.set(0.82, 1.08, 0.48)
    shieldGroup.add(shield)
    const shieldCore = new THREE.Mesh(new THREE.IcosahedronGeometry(0.2, 1), mintMaterial)
    shieldGroup.add(shieldCore)
    const shieldOrbit = new THREE.Mesh(new THREE.TorusGeometry(0.55, 0.025, 10, 52), cyanMaterial)
    shieldOrbit.rotation.x = 0.9
    shieldGroup.add(shieldOrbit)

    const scanMaterial = new THREE.MeshBasicMaterial({
      color: 0x65eef5,
      opacity: 0.13,
      transparent: true,
      side: THREE.DoubleSide,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    })
    const scanPlane = new THREE.Mesh(new THREE.PlaneGeometry(2.14, 0.18), scanMaterial)
    scanPlane.position.set(0, 0.08, 0.14)
    workstation.add(scanPlane)

    const sideKiosks: THREE.Group[] = []
    const createSideKiosk = (x: number, z: number, rotation: number) => {
      const group = new THREE.Group()
      group.position.set(x, -0.16, z)
      group.rotation.y = rotation
      group.scale.setScalar(0.64)

      const body = new THREE.Mesh(new THREE.BoxGeometry(1.65, 1.02, 0.18), navyMaterial)
      body.position.y = 0.36
      group.add(body)
      const face = new THREE.Mesh(
        new THREE.PlaneGeometry(1.42, 0.78),
        new THREE.MeshBasicMaterial({
          color: 0x154a86,
          transparent: true,
          opacity: 0.86,
          toneMapped: false,
        }),
      )
      face.position.set(0, 0.36, 0.1)
      group.add(face)
      const sideStand = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.72, 0.18), blueMaterial)
      sideStand.position.y = -0.48
      group.add(sideStand)
      const base = new THREE.Mesh(new THREE.BoxGeometry(0.92, 0.1, 0.46), navyMaterial)
      base.position.y = -0.86
      group.add(base)
      rootGroup.add(group)
      sideKiosks.push(group)
    }
    createSideKiosk(-2.18, -0.52, 0.2)
    createSideKiosk(2.18, -0.52, -0.2)

    const nodeMaterial = new THREE.MeshStandardMaterial({
      color: 0xc8fbff,
      emissive: 0x4dd8e7,
      emissiveIntensity: 0.9,
      metalness: 0.18,
      roughness: 0.18,
    })
    const floatingNodes: FloatingNode[] = []
    for (let index = 0; index < 8; index += 1) {
      const angle = (index / 8) * Math.PI * 2
      const radius = 2.4 + random() * 0.42
      const baseScale = 0.7 + random() * 0.55
      const node = new THREE.Mesh(new THREE.SphereGeometry(0.055, 14, 14), nodeMaterial)
      node.position.set(
        Math.cos(angle) * radius,
        -0.05 + Math.sin(angle * 1.4) * 0.85,
        0.2 + Math.sin(angle) * 0.45,
      )
      node.scale.setScalar(baseScale)
      rootGroup.add(node)
      floatingNodes.push({ mesh: node, phase: index * 0.78, baseScale })
    }

    const particleCount = 42
    const particlePositions = new Float32Array(particleCount * 3)
    for (let index = 0; index < particleCount; index += 1) {
      particlePositions[index * 3] = (random() - 0.5) * 6.8
      particlePositions[index * 3 + 1] = (random() - 0.25) * 3.7
      particlePositions[index * 3 + 2] = (random() - 0.5) * 2.8 - 0.5
    }
    const particleGeometry = new THREE.BufferGeometry()
    particleGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3))
    const particles = new THREE.Points(
      particleGeometry,
      new THREE.PointsMaterial({
        color: 0x6cecf4,
        size: 0.04,
        opacity: 0.48,
        transparent: true,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
      }),
    )
    rootGroup.add(particles)

    const pointerTarget = new THREE.Vector2()
    const pointerCurrent = new THREE.Vector2()
    const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)')
    let reduceMotion = motionPreference.matches
    let isDocumentVisible = document.visibilityState !== 'hidden'
    let isInViewport = true
    let mountRect = mount.getBoundingClientRect()
    const startedAt = performance.now()

    const resetPose = () => {
      pointerTarget.set(0, 0)
      pointerCurrent.set(0, 0)
      rootGroup.position.set(0.1, -0.35, 0)
      rootGroup.rotation.set(-0.015, 0, 0)
      workstation.position.set(0, -0.02, 0.35)
      scanPlane.position.y = 0.08
    }

    const renderFrame = (time = performance.now()) => {
      const elapsed = (time - startedAt) * 0.001
      const isChecking = stateRef.current === 'checking'
      const isReady = stateRef.current === 'ready'

      if (!reduceMotion && isDocumentVisible && isInViewport) {
        pointerCurrent.lerp(pointerTarget, 0.055)
        rootGroup.position.x = 0.1 + pointerCurrent.x * 0.16
        rootGroup.position.y = -0.35 - pointerCurrent.y * 0.08
        rootGroup.rotation.y = pointerCurrent.x * 0.12
        rootGroup.rotation.x = -0.015 - pointerCurrent.y * 0.055

        workstation.position.y = -0.02 + Math.sin(elapsed * 0.9) * 0.035
        sideKiosks[0].position.y = -0.16 + Math.sin(elapsed * 0.72 + 0.8) * 0.025
        sideKiosks[1].position.y = -0.16 + Math.sin(elapsed * 0.72 + 2.1) * 0.025
        shieldGroup.rotation.y = elapsed * 0.5
        shieldOrbit.rotation.z = elapsed * 0.75
        particles.rotation.y = elapsed * 0.035
        platformGlow.rotation.z = elapsed * 0.08

        const scanSpeed = isChecking ? 2.5 : isReady ? 1.35 : 0.75
        scanPlane.position.y = -0.02 + ((Math.sin(elapsed * scanSpeed) + 1) / 2) * 1.02
        scanMaterial.opacity = isChecking ? 0.3 : isReady ? 0.2 : 0.11
        mintMaterial.emissiveIntensity = (isReady ? 1.02 : 0.58) + Math.sin(elapsed * 2.2) * 0.08
        cyanMaterial.emissiveIntensity = (isChecking ? 1.08 : 0.72) + Math.sin(elapsed * 1.9) * 0.09

        floatingNodes.forEach(({ mesh, phase, baseScale }) => {
          const pulse = 1 + Math.sin(elapsed * 1.55 + phase) * 0.16
          mesh.scale.setScalar(baseScale * pulse)
        })
      }

      renderer.render(scene, camera)
    }

    const syncAnimation = () => {
      const shouldAnimate = !reduceMotion && isDocumentVisible && isInViewport
      renderer.setAnimationLoop(shouldAnimate ? renderFrame : null)
      if (!shouldAnimate) {
        resetPose()
        renderFrame()
      }
    }

    const handlePointerMove = (event: PointerEvent) => {
      if (reduceMotion || !isInViewport || mountRect.width === 0 || mountRect.height === 0) return
      const relativeX = (event.clientX - mountRect.left) / mountRect.width
      const relativeY = (event.clientY - mountRect.top) / mountRect.height
      pointerTarget.set((relativeX - 0.5) * 2, (relativeY - 0.5) * 2)
    }

    const handlePointerLeave = () => pointerTarget.set(0, 0)

    const handleViewportChange = () => {
      mountRect = mount.getBoundingClientRect()
    }

    const resizeScene = () => {
      mountRect = mount.getBoundingClientRect()
      const { width, height } = mountRect
      if (width === 0 || height === 0) return
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
      renderer.setSize(width, height, false)
      camera.aspect = width / height
      camera.updateProjectionMatrix()
      renderFrame()
    }

    const handleVisibilityChange = () => {
      isDocumentVisible = document.visibilityState !== 'hidden'
      syncAnimation()
    }
    const handleMotionPreference = (event: MediaQueryListEvent) => {
      reduceMotion = event.matches
      syncAnimation()
    }

    const resizeObserver = new ResizeObserver(resizeScene)
    resizeObserver.observe(mount)
    const intersectionObserver = new IntersectionObserver(
      ([entry]) => {
        isInViewport = entry.isIntersecting
        syncAnimation()
      },
      { threshold: 0.05 },
    )
    intersectionObserver.observe(mount)

    mount.addEventListener('pointermove', handlePointerMove, { passive: true })
    mount.addEventListener('pointerleave', handlePointerLeave)
    window.addEventListener('scroll', handleViewportChange, { passive: true })
    document.addEventListener('visibilitychange', handleVisibilityChange)
    motionPreference.addEventListener('change', handleMotionPreference)
    resizeScene()
    syncAnimation()

    return () => {
      renderer.setAnimationLoop(null)
      resizeObserver.disconnect()
      intersectionObserver.disconnect()
      mount.removeEventListener('pointermove', handlePointerMove)
      mount.removeEventListener('pointerleave', handlePointerLeave)
      window.removeEventListener('scroll', handleViewportChange)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
      motionPreference.removeEventListener('change', handleMotionPreference)

      const geometries = new Set<THREE.BufferGeometry>()
      const materials = new Set<THREE.Material>()
      scene.traverse((object) => {
        const renderable = object as THREE.Object3D & {
          geometry?: THREE.BufferGeometry
          material?: THREE.Material | THREE.Material[]
        }
        if (renderable.geometry) geometries.add(renderable.geometry)
        if (Array.isArray(renderable.material)) {
          renderable.material.forEach((material) => materials.add(material))
        } else if (renderable.material) {
          materials.add(renderable.material)
        }
      })
      geometries.forEach((geometry) => geometry.dispose())
      materials.forEach((material) => material.dispose())
      screenTexture?.dispose()
      renderer.dispose()
      renderer.forceContextLoss()

      if (renderer.domElement.parentElement === mount) {
        mount.removeChild(renderer.domElement)
      }
    }
  }, [kioskNumber, roomLabel])

  return <div ref={mountRef} className="size-full touch-pan-y" aria-hidden="true" />
}
